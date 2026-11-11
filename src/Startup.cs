using System.Drawing.Drawing2D;
using System.Text.Json;

namespace PartitionStudio;

public static class Brand
{
    static readonly Lazy<Image> logo=new(()=>{
        using var stream=typeof(Brand).Assembly.GetManifestResourceStream("PartitionStudio.BrandMark.png")??throw new InvalidOperationException("Recurso do logótipo em falta.");
        using var original=Image.FromStream(stream);return new Bitmap(original);
    });
    static readonly Lazy<Icon> icon=new(()=>{
        using var stream=typeof(Brand).Assembly.GetManifestResourceStream("PartitionStudio.AppIcon.ico")??throw new InvalidOperationException("Ícone da aplicação em falta.");
        using var original=new Icon(stream);return (Icon)original.Clone();
    });
    public static Image Logo=>logo.Value;
    public static Icon Icon=>(Icon)icon.Value.Clone();
}

public static class DiskTopology
{
    public static string HardwareFingerprint(IEnumerable<Disk> disks)=>HardwareFingerprint(disks.Select(d=>new HardwareDisk(d.Number,d.UniqueId,d.Serial,d.Size,d.Style,d.Offline,d.ReadOnly,d.Boot,d.System)));
    public static string HardwareFingerprint(IEnumerable<HardwareDisk> disks)=>JsonSerializer.Serialize(disks.OrderBy(d=>d.Number));
    public static string Fingerprint(IEnumerable<Disk> disks)=>JsonSerializer.Serialize(disks.OrderBy(d=>d.Number).Select(d=>new {
        d.Number,d.UniqueId,d.Serial,d.Size,d.Style,d.Offline,d.ReadOnly,d.Boot,d.System,
        Parts=d.Partitions.OrderBy(p=>p.Number).Select(p=>new{p.Number,p.Offset,p.Size,p.Letter,p.FileSystem,p.GptType,p.MbrType,p.Hidden,p.Boot,p.System})
    }));
    public static bool Changed(IEnumerable<Disk> before,IEnumerable<Disk> after)=>Fingerprint(before)!=Fingerprint(after);
}
public record HardwareDisk(int Number,string UniqueId,string Serial,long Size,string Style,bool Offline,bool ReadOnly,bool Boot,bool System);

public sealed class StartupWindow : Form
{
    readonly Label detail=Ui.Label("A preparar a aplicação…",11),error=Ui.Label("",9);
    readonly ActivityIndicator spinner=new(){Active=true};readonly Button retry;
    readonly Func<Task<Disk[]>> load;readonly Action<Disk[]> ready;bool loading;
    public StartupWindow(Func<Task<Disk[]>> load,Action<Disk[]> ready)
    {
        this.load=load;this.ready=ready;
        Text="Partition Studio · A iniciar";Icon=Brand.Icon;ClientSize=new Size(600,430);FormBorderStyle=FormBorderStyle.FixedSingle;
        MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Ui.Navy;Font=new Font("Segoe UI",10);DoubleBuffered=true;
        var logo=new PictureBox{Image=Brand.Logo,SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(230,33,140,140),BackColor=Ui.Navy};Controls.Add(logo);
        var name=Ui.Label("Partition Studio",25,true);name.ForeColor=Color.White;name.Location=new Point(171,179);Controls.Add(name);
        var subtitle=Ui.Label("Os seus discos. Tudo no lugar.",11);subtitle.ForeColor=Color.FromArgb(159,177,207);subtitle.Location=new Point(190,231);Controls.Add(subtitle);
        spinner.BackColor=Ui.Navy;spinner.Location=new Point(58,282);Controls.Add(spinner);
        detail.AutoSize=false;detail.Size=new Size(475,29);detail.ForeColor=Color.FromArgb(219,230,250);detail.Location=new Point(91,284);Controls.Add(detail);
        var note=Ui.Label("Leitura inicial de discos e partições · sem alterações nos discos",9);note.ForeColor=Color.FromArgb(140,159,191);note.Location=new Point(58,325);Controls.Add(note);
        error.AutoSize=false;error.ForeColor=Color.FromArgb(255,183,173);error.Bounds=new Rectangle(58,354,480,28);error.AutoEllipsis=true;Controls.Add(error);
        retry=Ui.Button("Tentar novamente",async (_,_)=>await BeginLoad(),true);retry.Visible=false;retry.Location=new Point(58,384);Controls.Add(retry);
        Shown+=async (_,_)=>await BeginLoad();
    }
    async Task BeginLoad()
    {
        if(loading)return;loading=true;retry.Visible=false;error.Text="";spinner.Active=true;detail.Text="A detetar discos e a ler as partições…";
        try{
            var disks=await load();if(IsDisposed||Disposing)return;
            detail.Text=$"{disks.Length} discos detetados · a abrir a área de trabalho…";
            await Task.Yield();if(!IsDisposed&&!Disposing)ready(disks);
        }catch(Exception ex){if(!IsDisposed&&!Disposing){detail.Text="Não foi possível carregar os discos.";error.Text=ex.Message;retry.Visible=true;spinner.Active=false;}}
        finally{loading=false;}
    }
    public static Disk[] Sample()=>[new Disk(1,"Disco de teste — pré-visualização","sample","sample",256L<<30,"GPT",false,false,false,false,"Healthy",75L<<30,[])];
}

public sealed class StudioApplicationContext : ApplicationContext
{
    readonly StartupWindow startup;
    public StudioApplicationContext()
    {
        startup=new StartupWindow(async ()=>JsonSerializer.Deserialize<Disk[]>(await Backend.Run(new("Inventory")))??[],OpenMain);
        MainForm=startup;startup.Show();
    }
    void OpenMain(Disk[] disks)
    {
        var main=new MainWindow(initialDisks:disks);
        try{main.Show();MainForm=main;startup.Close();startup.Dispose();}catch{main.Dispose();throw;}
    }
}
