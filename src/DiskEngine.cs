using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace PartitionStudio;

public record DeviceState(int Number,bool Offline,bool ReadOnly,bool Boot,bool System,int LogicalSectorSize,int PhysicalSectorSize,long Size,string Style);
public record TransferProgress(string Stage,long Done,long Total);
public record ImageManifest(int Version,long Size,int LogicalSectorSize,string Sha256,string Source,DateTimeOffset Created);
public record SectorIssue(long Offset,int Length,string Error);
public record ScanResult(long Total,long Checked,bool Complete,List<SectorIssue> Issues,string? Failure=null);

public static class DiskEngine
{
    public static async Task<DeviceState> Inspect(Disk disk)=>JsonSerializer.Deserialize<DeviceState>(await Backend.Run(new("Inspect",disk),"Advanced.ps1"))??throw new IOException("Não foi possível validar o disco.");
    public static void Validate(DeviceState state,bool writing,bool offline)
    {
        if(state.Size<=0||state.LogicalSectorSize is not (512 or 4096)||state.Size%state.LogicalSectorSize!=0)throw new IOException("Geometria do disco não suportada.");
        if(offline&&(!state.Offline||state.Boot||state.System))throw new IOException("A cópia requer um disco secundário offline. O Windows em execução está protegido.");
        if(writing&&state.ReadOnly)throw new IOException("Disco de destino protegido contra escrita.");
    }
    public static void ValidateClone(DeviceState source,DeviceState target)
    {
        Validate(source,false,true);Validate(target,true,true);
        if(source.Number==target.Number)throw new IOException("Origem e destino não podem ser o mesmo disco.");
        if(source.Size!=target.Size||source.LogicalSectorSize!=target.LogicalSectorSize)throw new IOException("Esta versão requer discos com capacidade exata e setor lógico iguais. Não redimensiona GPT/partições durante a clonagem.");
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeviceIoControl(SafeFileHandle handle,uint code,IntPtr input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
    static FileStream OpenPhysical(DeviceState state,bool write)
    {
        if(!Backend.Admin)throw new UnauthorizedAccessException("Abra a aplicação como administrador.");
        var stream=new FileStream(@"\\.\PhysicalDrive"+state.Number,FileMode.Open,write?FileAccess.ReadWrite:FileAccess.Read,FileShare.ReadWrite,1024*1024,FileOptions.None);
        try{
            var number=new byte[12];var length=new byte[8];
            if(!DeviceIoControl(stream.SafeFileHandle,0x2D1080,IntPtr.Zero,0,number,number.Length,out _,IntPtr.Zero)||BitConverter.ToInt32(number,4)!=state.Number)throw new IOException("O identificador do dispositivo aberto não corresponde ao disco.");
            if(!DeviceIoControl(stream.SafeFileHandle,0x7405c,IntPtr.Zero,0,length,length.Length,out _,IntPtr.Zero)||BitConverter.ToInt64(length)!=state.Size)throw new IOException("A capacidade do dispositivo aberto mudou.");
            return stream;
        }catch{stream.Dispose();throw;}
    }
    static void Report(IProgress<TransferProgress>? progress,string stage,long done,long total)=>progress?.Report(new(stage,done,total));
    public static string CopyExact(Stream input,Stream output,long count,CancellationToken token,IProgress<TransferProgress>? progress=null,string stage="A copiar")
    {
        if(count<=0)throw new ArgumentOutOfRangeException(nameof(count));
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[1024*1024];long done=0;var watch=System.Diagnostics.Stopwatch.StartNew();
        while(done<count){token.ThrowIfCancellationRequested();int want=(int)Math.Min(buffer.Length,count-done);int read=input.Read(buffer,0,want);if(read==0)throw new EndOfStreamException("A origem terminou antes da capacidade esperada. A cópia ficou incompleta.");output.Write(buffer,0,read);hash.AppendData(buffer,0,read);done+=read;if(watch.ElapsedMilliseconds>150){Report(progress,stage,done,count);watch.Restart();}}
        output.Flush();Report(progress,stage,done,count);return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static string HashExact(Stream input,long count,CancellationToken token,IProgress<TransferProgress>? progress=null,string stage="A verificar SHA-256")
    {
        using var sink=Stream.Null;return CopyExact(input,sink,count,token,progress,stage);
    }
    static void CheckHash(string actual,string expected){if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("A verificação SHA-256 falhou. Não considere a cópia válida.");}
    public static async Task<string> Backup(Disk source,string path,CancellationToken token,IProgress<TransferProgress> progress)
    {
        var state=await Inspect(source);Validate(state,false,true);
        await Backend.Run(new("BackupTarget",source,Size:source.Size,Path:path),"Advanced.ps1");
        path=Path.GetFullPath(path);if(File.Exists(path)||File.Exists(path+".json"))throw new IOException("O destino já existe. Escolha um nome novo.");
        string temp=path+".partial-"+Guid.NewGuid().ToString("N");
        return await Task.Run(async ()=>{
            bool complete=false;
            try{
                using var input=OpenPhysical(state,false);Validate(await Inspect(source),false,true);
                string hash;
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,1024*1024)){
                    hash=CopyExact(input,output,state.Size,token,progress,"A criar imagem completa");output.Flush(true);output.Position=0;CheckHash(HashExact(output,state.Size,token,progress,"A verificar imagem gravada"),hash);
                }
                var manifest=new ImageManifest(1,state.Size,state.LogicalSectorSize,hash,source.Name,DateTimeOffset.UtcNow);
                File.WriteAllText(temp+".json",JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));
                File.Move(temp,path,false);File.Move(temp+".json",path+".json",false);complete=true;
                return "Imagem criada e verificada: "+path+". Guarde também o ficheiro .json.";
            }finally{if(!complete)Report(progress,"Backup incompleto: os ficheiros .partial não são backups válidos.",0,0);}
        });
    }
    public static ImageManifest ReadManifest(string path)
    {
        var m=JsonSerializer.Deserialize<ImageManifest>(File.ReadAllText(path+".json"))??throw new IOException("Metadados da imagem em falta.");
        if(m.Version!=1||m.Size<=0||m.LogicalSectorSize is not (512 or 4096)||m.Size%m.LogicalSectorSize!=0||m.Sha256 is null||!System.Text.RegularExpressions.Regex.IsMatch(m.Sha256,"\\A[0-9A-Fa-f]{64}\\z")||new FileInfo(path).Length!=m.Size)throw new IOException("Imagem ou metadados inválidos/incompletos.");
        return m;
    }
    public static async Task<string> Restore(string path,Disk target,CancellationToken token,IProgress<TransferProgress> progress)
    {
        var m=ReadManifest(path);var state=await Inspect(target);Validate(state,true,true);
        if(state.Size!=m.Size||state.LogicalSectorSize!=m.LogicalSectorSize)throw new IOException("Destino e imagem têm de ter capacidade exata e setor lógico iguais.");
        return await Task.Run(async ()=>{
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024);
            CheckHash(HashExact(input,m.Size,token,progress,"A validar imagem antes de escrever no destino"),m.Sha256);input.Position=0;token.ThrowIfCancellationRequested();
            using var output=OpenPhysical(state,true);Validate(await Inspect(target),true,true);
            CheckHash(CopyExact(input,output,m.Size,token,progress,"A restaurar — destino a ser substituído"),m.Sha256);output.Flush(true);output.Position=0;
            CheckHash(HashExact(output,m.Size,token,progress,"A verificar destino restaurado"),m.Sha256);
            return "Imagem restaurada e verificada. O disco permanece offline. Desligue-o antes de iniciar o sistema copiado.";
        });
    }
    public static async Task<string> Clone(Disk source,Disk target,CancellationToken token,IProgress<TransferProgress> progress)
    {
        var a=await Inspect(source);var b=await Inspect(target);ValidateClone(a,b);
        return await Task.Run(async ()=>{
            using var input=OpenPhysical(a,false);using var output=OpenPhysical(b,true);
            ValidateClone(await Inspect(source),await Inspect(target));
            string hash=CopyExact(input,output,a.Size,token,progress,"A clonar — destino a ser substituído");output.Flush(true);output.Position=0;
            CheckHash(HashExact(output,a.Size,token,progress,"A verificar disco clonado"),hash);
            return "Disco clonado e verificado. Ambos os discos permanecem offline. Discos clonados têm identificadores de partições iguais; não coloque os dois online sem compreender os conflitos.";
        });
    }
    public static ScanResult ScanCore(Stream input,long size,int sector,CancellationToken token,IProgress<TransferProgress>? progress=null)
    {
        var issues=new List<SectorIssue>();var buffer=new byte[1024*1024];long done=0;int consecutive=0;var watch=System.Diagnostics.Stopwatch.StartNew();
        try{
            while(done<size){token.ThrowIfCancellationRequested();int want=(int)Math.Min(buffer.Length,size-done);
                try{input.Position=done;input.ReadExactly(buffer,0,want);done+=want;consecutive=0;}
                catch(IOException){
                    long end=done+want;
                    while(done<end){token.ThrowIfCancellationRequested();int n=(int)Math.Min(sector,end-done);
                        try{input.Position=done;input.ReadExactly(buffer,0,n);consecutive=0;}catch(IOException ex){issues.Add(new(done,n,ex.Message));consecutive++;}
                        done+=n;if(consecutive>=256||issues.Count>=10000)throw new IOException("Pesquisa interrompida pelo limite de erros (256 consecutivos ou 10000 no total). O dispositivo pode ter falhado ou sido desligado.");
                    }
                }
                if(watch.ElapsedMilliseconds>150){Report(progress,"A ler setores — sem escritas",done,size);watch.Restart();}
            }
            Report(progress,"Leitura concluída",done,size);return new(size,done,true,issues);
        }catch(OperationCanceledException){return new(size,done,false,issues,"Interrompido pelo utilizador.");}
        catch(IOException ex){return new(size,done,false,issues,ex.Message);}
    }
    public static async Task<ScanResult> Scan(Disk source,CancellationToken token,IProgress<TransferProgress> progress)
    {
        var state=await Inspect(source);Validate(state,false,false);
        return await Task.Run(async ()=>{using var input=OpenPhysical(state,false);await Inspect(source);return ScanCore(input,state.Size,state.LogicalSectorSize,token,progress);});
    }
}
