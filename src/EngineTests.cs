using System.Security.Cryptography;
using System.Text.Json;

namespace PartitionStudio;

public static class EngineTests
{
    public static int Run(string directory)
    {
        var results=new List<string>();
        try{
            if(!Path.IsPathFullyQualified(directory))throw new ArgumentException("Indique uma pasta absoluta de testes.");Directory.CreateDirectory(directory);
            void Check(bool condition,string label){if(!condition)throw new Exception(label);results.Add("PASS: "+label);}
            void Reject(Action work,string label){try{work();}catch(IOException){results.Add("PASS: "+label);return;}throw new Exception("Guard failed: "+label);}
            var a=new DeviceState(7,true,false,false,false,512,4096,2L<<20,"GPT");var b=a with{Number=8};
            DiskEngine.ValidateClone(a,b);Check(true,"Discos offline compatíveis aceites");
            Reject(()=>DiskEngine.ValidateClone(a,a),"Recusa origem igual ao destino");
            Reject(()=>DiskEngine.ValidateClone(a,b with{Offline=false}),"Recusa destino online");
            Reject(()=>DiskEngine.ValidateClone(a with{Boot=true},b),"Recusa origem de arranque");
            Reject(()=>DiskEngine.ValidateClone(a,b with{System=true}),"Recusa destino de sistema");
            Reject(()=>DiskEngine.ValidateClone(a,b with{ReadOnly=true}),"Recusa destino só de leitura");
            Reject(()=>DiskEngine.ValidateClone(a,b with{Size=4L<<20}),"Recusa capacidades diferentes, incluindo destino maior");
            Reject(()=>DiskEngine.ValidateClone(a,b with{LogicalSectorSize=4096}),"Recusa tamanhos de setor diferentes");
            byte[] bytes=new byte[2*1024*1024];new Random(42).NextBytes(bytes);string path=Path.Combine(directory,"fixture.psimg");
            using(var input=new MemoryStream(bytes))using(var output=new FileStream(path,FileMode.Create,FileAccess.ReadWrite)){
                string hash=DiskEngine.CopyExact(input,output,bytes.Length,CancellationToken.None);Check(hash==Convert.ToHexString(SHA256.HashData(bytes)),"Cópia completa tem SHA-256 idêntico");
                output.Position=0;Check(DiskEngine.HashExact(output,bytes.Length,CancellationToken.None)==hash,"Leitura de verificação do destino");
            }
            using(var shortInput=new MemoryStream(new byte[512]))using(var output=new MemoryStream())Reject(()=>DiskEngine.CopyExact(shortInput,output,1024,CancellationToken.None),"Deteta origem truncada");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();using var input=new MemoryStream(bytes);using var output=new MemoryStream();try{DiskEngine.CopyExact(input,output,bytes.Length,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(output.Length==0,"Cancelamento antes da escrita não altera o destino");}}
            var manifest=new ImageManifest(1,bytes.Length,512,Convert.ToHexString(SHA256.HashData(bytes)),"fixture",DateTimeOffset.UtcNow);
            File.WriteAllText(path+".json",JsonSerializer.Serialize(manifest));Check(DiskEngine.ReadManifest(path).Size==bytes.Length,"Metadados e tamanho da imagem validados");
            File.WriteAllText(path+".json",JsonSerializer.Serialize(manifest with{Size=512}));Reject(()=>DiskEngine.ReadManifest(path),"Recusa imagem com tamanho diferente dos metadados");
            File.WriteAllText(path+".json",JsonSerializer.Serialize(manifest with{Sha256="invalido"}));Reject(()=>DiskEngine.ReadManifest(path),"Recusa hash inválido nos metadados");
            using(var clean=new MemoryStream(bytes)){var scan=DiskEngine.ScanCore(clean,bytes.Length,512,CancellationToken.None);Check(scan.Complete&&scan.Issues.Count==0&&scan.Checked==bytes.Length,"Pesquisa completa sem erros em imagem");}
            using(var faulty=new FaultStream(bytes,4096)){var scan=DiskEngine.ScanCore(faulty,bytes.Length,512,CancellationToken.None);Check(scan.Complete&&scan.Issues.Count==1&&scan.Issues[0].Offset==4096&&scan.Issues[0].Length==512,"Localiza setor defeituoso simulado sem saltar o resto do bloco");}
            using(var failing=new FaultStream(bytes,0,bytes.Length)){var scan=DiskEngine.ScanCore(failing,bytes.Length,512,CancellationToken.None);Check(!scan.Complete&&scan.Issues.Count==256&&scan.Failure is not null,"Interrompe após 256 erros consecutivos e preserva relatório parcial");}
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();using var input=new MemoryStream(bytes);var scan=DiskEngine.ScanCore(input,bytes.Length,512,cancelled.Token);Check(!scan.Complete&&scan.Checked==0,"Pesquisa cancelada é marcada incompleta");}
            var part=new Part(1,1048576,180L<<30,"D","Arquivo · exemplo","NTFS",85L<<30,false,false,false,"Basic","{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}",0);
            var disk=new Disk(1,"Disco de teste — apenas pré-visualização","sample","sample",256L<<30,"GPT",false,false,false,false,"Healthy",75L<<30,[part]);
            using(var form=new ToolsWindow([disk],disk,true)){
                form.Show();Application.DoEvents();
                for(int i=0;i<3;i++){form.SelectPreviewTab(i);Application.DoEvents();using var screenshot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(screenshot,new Rectangle(0,0,form.Width,form.Height));screenshot.Save(Path.Combine(directory,$"ferramentas-{i}.png"));}form.Close();
            }
            results.Add("PASS: três separadores de ferramentas renderizados com dados artificiais");
            results.Add("Nenhum disco físico foi aberto nem alterado nestes testes. Acesso RAW e wbadmin não foram executados.");
            File.WriteAllLines(Path.Combine(directory,"extended-tests.txt"),results);return 0;
        }catch(Exception ex){results.Add("FAIL: "+ex);try{File.WriteAllLines(Path.Combine(directory,"extended-tests.txt"),results);}catch{}return 1;}
    }
    sealed class FaultStream(byte[] bytes,long badOffset,long badLength=512):MemoryStream(bytes)
    {
        void Guard(int count){if(Position<badOffset+badLength&&Position+count>badOffset)throw new IOException("Erro de leitura de setor simulado.");}
        public override int Read(byte[] buffer,int offset,int count){Guard(count);return base.Read(buffer,offset,count);}
        public override int Read(Span<byte> buffer){Guard(buffer.Length);return base.Read(buffer);}
    }
}
