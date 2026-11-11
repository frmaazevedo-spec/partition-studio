using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace PartitionStudio;

public record Part(int Number, long Offset, long Size, string Letter, string Label, string FileSystem,
    long Free, bool Boot, bool System, bool Hidden, string Type, string GptType, int MbrType)
{
    public string Title => string.IsNullOrWhiteSpace(Letter) ? $"Partição {Number}" : $"{Letter}:  {Label}";
}
public record Disk(int Number, string Name, string UniqueId, string Serial, long Size, string Style,
    bool Boot, bool System, bool Offline, bool ReadOnly, string Health, long LargestFree, Part[] Partitions)
{
    public bool Protected => Boot || System || Offline || ReadOnly || Style is not ("GPT" or "MBR");
}
public record Request(string Action, Disk? Disk = null, Part? Partition = null, long Size = 0,
    string FileSystem = "NTFS", string Label = "Dados", string Path = "", bool Flag = false)
{
    public override string ToString() => $"{Names.GetValueOrDefault(Action, Action)} · Disco {Disk?.Number}" +
        (Partition is null ? "" : $" · {Partition.Title}") + (Size > 0 ? $" → {Ui.Bytes(Size)}" : "") +
        (Action == "Format" ? $" · {FileSystem} · {Label}" : "");
    public static readonly Dictionary<string,string> Names = new() { ["Create"]="Criar partição", ["Resize"]="Redimensionar",
        ["Format"]="Formatar", ["Delete"]="Eliminar partição", ["Repair"]="Reparar volume" };
}
public static class Backend
{
    public static bool Admin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public static string Script => new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("PartitionStudio.Storage.ps1")!).ReadToEnd();
    public static async Task<string> Run(Request request, string resource = "Storage.ps1")
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)));
        using var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("PartitionStudio."+resource)!);
        string command = "& {" + reader.ReadToEnd() + "} -RequestBase64 '" + payload + "'";
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")) {
            UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true, RedirectStandardInput=true,
            StandardOutputEncoding=Encoding.UTF8, StandardErrorEncoding=Encoding.UTF8 };
        string loader="[Console]::InputEncoding = [Text.UTF8Encoding]::new($false); $code = [Console]::In.ReadToEnd(); & ([ScriptBlock]::Create($code))";
        foreach (var arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(loader))}) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new Exception("Não foi possível iniciar o Windows PowerShell.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(command); process.StandardInput.Close();
        await process.WaitForExitAsync();
        string output = await stdout, error = await stderr;
        if(process.ExitCode != 0) throw new Exception(string.IsNullOrWhiteSpace(error) ? output : error);
        return output.Trim();
    }
    public static bool Eligible(Disk d, Part? p, string action)
    {
        if(d.Protected) return false;
        if(action == "Create") return d.LargestFree >= 16L * 1024 * 1024;
        if(p is null || p.Boot || p.System || p.Hidden) return false;
        bool basic = d.Style == "GPT" ? p.GptType.Equals("{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}", StringComparison.OrdinalIgnoreCase) : new[]{7,11,12,6,14}.Contains(p.MbrType);
        if(!basic) return false;
        if(action is "Resize" or "Scan" or "Repair") return p.FileSystem == "NTFS" && !string.IsNullOrWhiteSpace(p.Letter);
        return action == "Format" || !string.IsNullOrWhiteSpace(p.Letter);
    }
}
public class MainWindow : Form
{
    readonly DataGridView grid = new();
    readonly FlowLayoutPanel maps=new(), actions=new(), recovery=new();
    readonly ListBox queue=new(); readonly TextBox log=new(); readonly Label status=Ui.Label("A iniciar…"), summary=Ui.Label("Discos e partições",20,true);
    readonly Button apply, refresh, advanced; readonly List<Request> pending=new(); readonly List<Button> operationButtons=new();
    readonly Metric diskMetric=new(){Caption="DISCOS LIGADOS",IconKey="disk"}, capacityMetric=new(){Caption="CAPACIDADE TOTAL",IconKey="queue",Accent=Ui.Teal}, partitionMetric=new(){Caption="PARTIÇÕES",IconKey="shield",Accent=Color.FromArgb(153,108,232)};
    readonly ActivityIndicator activity=new();
    readonly System.Windows.Forms.Timer entrance=new(){Interval=16};
    Disk[] disks=[]; Disk? selectedDisk; Part? selectedPart; bool busy; readonly bool smoke;
    public MainWindow(bool smoke=false)
    {
        this.smoke=smoke;
        Text="Partition Studio · Gestão e recuperação"; Width=1460; Height=1000; MinimumSize=new Size(1250,950);
        DoubleBuffered=true;StartPosition=FormStartPosition.CenterScreen;BackColor=Ui.Bg;ForeColor=Ui.Ink;Font=new Font("Segoe UI",10);
        using(var mark=Ui.IconBitmap("disk",Ui.Blue,32))Icon=(Icon)Icon.FromHandle(mark.GetHicon()).Clone();
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(16)};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,258));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,94));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(root);
        var brand=new Panel{Dock=DockStyle.Fill,BackColor=Ui.Navy,Margin=Padding.Empty,Padding=new Padding(19)};
        var logo=new PictureBox{Image=Ui.IconBitmap("disk",Color.FromArgb(154,178,255),32),Bounds=new Rectangle(20,20,32,32)};brand.Controls.Add(logo);
        var title=Ui.Label("Partition Studio",15,true);title.ForeColor=Color.White;title.Location=new Point(61,19);brand.Controls.Add(title);
        var edition=Ui.Label("GESTÃO & RECUPERAÇÃO",8);edition.ForeColor=Color.FromArgb(139,156,187);edition.Location=new Point(22,58);brand.Controls.Add(edition);root.Controls.Add(brand,0,0);
        var header=new Panel{Dock=DockStyle.Fill,Margin=new Padding(22,0,0,0)};summary.Text="O seu armazenamento, organizado.";summary.Font=new Font("Segoe UI",21,FontStyle.Bold);summary.Location=new Point(0,8);header.Controls.Add(summary);
        var subtitle=Ui.Label("Visão geral  /  Discos e partições",10);subtitle.ForeColor=Ui.Muted;subtitle.Location=new Point(2,53);header.Controls.Add(subtitle);
        var motion=new CheckBox{Text="Animações",AutoSize=true,Checked=Ui.MotionEnabled,Anchor=AnchorStyles.Top|AnchorStyles.Right,ForeColor=Ui.Muted};
        motion.Location=new Point(880,59);motion.CheckedChanged+=(_,_)=>{Ui.MotionEnabled=motion.Checked;if(!Ui.MotionEnabled){entrance.Stop();Opacity=1;}activity.Active=busy;};header.Controls.Add(motion);
        header.Resize+=(_,_)=>motion.Left=Math.Max(0,header.ClientSize.Width-motion.Width-10);root.Controls.Add(header,1,0);
        var side=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,BackColor=Ui.Navy,Padding=new Padding(18,15,16,10),Margin=Padding.Empty};
        side.RowStyles.Add(new RowStyle(SizeType.Absolute,30));side.RowStyles.Add(new RowStyle(SizeType.Absolute,300));side.RowStyles.Add(new RowStyle(SizeType.Absolute,34));side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
        Label SideLabel(string text){var l=Ui.Label(text,8.5f,true);l.ForeColor=Color.FromArgb(143,160,189);return l;}
        side.Controls.Add(SideLabel("FERRAMENTAS DO DISCO"),0,0);
        actions.Dock=DockStyle.Fill;actions.FlowDirection=FlowDirection.TopDown;actions.WrapContents=false;actions.BackColor=Ui.Navy;
        foreach(var key in new[]{"Create","Resize","Format","Delete","Repair"}){
            var action=key;var b=(MotionButton)Ui.Button(Request.Names[key],async (_,_)=>await Stage(action));b.Dark=true;b.AutoSize=false;b.Width=221;b.Height=42;actions.Controls.Add(b);operationButtons.Add(b);
        }
        var scan=(MotionButton)Ui.Button("Verificar ficheiros",async (_,_)=>await Scan());scan.Dark=true;scan.AutoSize=false;scan.Width=221;scan.Height=42;actions.Controls.Add(scan);operationButtons.Add(scan);
        side.Controls.Add(actions,0,1);side.Controls.Add(SideLabel("ALTERAÇÕES PENDENTES"),0,2);
        var qpanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,BackColor=Ui.Navy};
        qpanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));qpanel.RowStyles.Add(new RowStyle(SizeType.Absolute,50));qpanel.RowStyles.Add(new RowStyle(SizeType.Absolute,50));qpanel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        queue.Dock=DockStyle.Fill;queue.HorizontalScrollbar=true;queue.BorderStyle=BorderStyle.None;queue.BackColor=Color.FromArgb(28,40,63);queue.ForeColor=Color.FromArgb(200,213,235);queue.DrawMode=DrawMode.OwnerDrawFixed;queue.ItemHeight=57;
        queue.DrawItem+=(_,e)=>{if(e.Index<0)return;e.DrawBackground();using var font=new Font("Segoe UI",9);TextRenderer.DrawText(e.Graphics,queue.Items[e.Index].ToString(),font,new Rectangle(e.Bounds.X+10,e.Bounds.Y+8,e.Bounds.Width-18,e.Bounds.Height-12),e.ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};qpanel.Controls.Add(queue,0,0);
        apply=Ui.Button("Aplicar alterações (0)",async (_,_)=>await Apply(),true);apply.Dock=DockStyle.Fill;qpanel.Controls.Add(apply,0,1);
        var remove=(MotionButton)Ui.Button("Remover da fila",(_,_)=>{if(!busy&&queue.SelectedIndex>=0){pending.RemoveAt(queue.SelectedIndex);UpdateQueue();}});remove.Dark=true;remove.Dock=DockStyle.Fill;qpanel.Controls.Add(remove,0,2);
        var note=Ui.Label("Uma operação por disco.\nReveja os destinos antes de aplicar.",8.5f);note.ForeColor=Color.FromArgb(139,156,187);qpanel.Controls.Add(note,0,3);side.Controls.Add(qpanel,0,3);
        var version=SideLabel("●  WINDOWS x64     /     v0.3.1-dev");version.ForeColor=Color.FromArgb(117,201,184);side.Controls.Add(version,0,4);root.Controls.Add(side,0,1);
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Margin=new Padding(20,0,0,0)};
        body.RowStyles.Add(new RowStyle(SizeType.Absolute,123));body.RowStyles.Add(new RowStyle(SizeType.Absolute,50));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,174));body.RowStyles.Add(new RowStyle(SizeType.Absolute,132));body.RowStyles.Add(new RowStyle(SizeType.Absolute,90));root.Controls.Add(body,1,1);
        var metrics=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,12)};
        for(int i=0;i<3;i++)metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
        metrics.Controls.Add(diskMetric,0,0);metrics.Controls.Add(capacityMetric,1,0);partitionMetric.Margin=Padding.Empty;metrics.Controls.Add(partitionMetric,2,0);body.Controls.Add(metrics,0,0);
        var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty};refresh=Ui.Button("Atualizar discos",async (_,_)=>await LoadDisks());toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(Ui.Button("Abrir como administrador",(_,_)=>Elevate()));toolbar.Controls.Add(Ui.Button("Exportar inventário",(_,_)=>Export()));advanced=Ui.Button("Saúde, backups e clonagem",async (_,_)=>await OpenTools(),true);toolbar.Controls.Add(advanced);body.Controls.Add(toolbar,0,1);
        Surface Card(string caption,Control content){var card=new Surface{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10),Padding=new Padding(14,43,14,12)};var label=Ui.Label(caption,10,true);label.BackColor=Color.White;label.Location=new Point(17,13);card.Controls.Add(label);content.Dock=DockStyle.Fill;card.Controls.Add(content);return card;}
        grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.AllowUserToResizeRows=false;
        grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.GridColor=Color.FromArgb(237,241,247);
        grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(246,248,252);grid.ColumnHeadersDefaultCellStyle.ForeColor=Ui.Muted;grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9);grid.ColumnHeadersHeight=36;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(246,248,252);grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Ui.Muted;
        grid.RowTemplate.Height=36;grid.DefaultCellStyle.Font=new Font("Segoe UI",9);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(234,239,255);grid.DefaultCellStyle.SelectionForeColor=Ui.Blue;
        foreach(var col in new[]{"Disco / partição","Sistema","Capacidade","Livre","Estado"})grid.Columns.Add(col,col);
        grid.Columns[0].FillWeight=205;grid.Columns[0].DefaultCellStyle.Padding=new Padding(30,0,0,0);grid.Columns[4].FillWeight=130;
        grid.CellPainting+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==0&&grid.Rows[e.RowIndex].Tag is ValueTuple<Disk,Part?> x){e.Paint(e.ClipBounds,DataGridViewPaintParts.All);Ui.DrawIcon(e.Graphics!,x.Item2 is null?"disk":x.Item2.Boot||x.Item2.System||x.Item2.Hidden?"shield":"file",new RectangleF(e.CellBounds.X+7,e.CellBounds.Y+10,16,16),x.Item2 is null?Ui.Blue:Ui.Muted);e.Handled=true;}};
        grid.SelectionChanged+=(_,_)=>SelectRow();body.Controls.Add(Card("Dispositivos e volumes",grid),0,2);
        maps.AutoScroll=true;maps.FlowDirection=FlowDirection.TopDown;maps.WrapContents=false;maps.BackColor=Color.White;maps.Margin=Padding.Empty;
        maps.Layout+=(_,_)=>{foreach(Control map in maps.Controls){int width=Math.Max(200,maps.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8);if(map.Width!=width)map.Width=width;}};body.Controls.Add(Card("Mapa de armazenamento",maps),0,3);
        recovery.Dock=DockStyle.Fill;recovery.FlowDirection=FlowDirection.TopDown;recovery.WrapContents=false;recovery.BackColor=Color.White;
        var recoveryButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,BackColor=Color.White,Margin=Padding.Empty};
        recoveryButtons.Controls.Add(Ui.Button("Recuperar ficheiros · PhotoRec",(_,_)=>Recovery(false),true));recoveryButtons.Controls.Add(Ui.Button("Recuperar partições · TestDisk",(_,_)=>Recovery(true)));recoveryButtons.Controls.Add(Ui.Button("Guia",(_,_)=>Help()));recovery.Controls.Add(recoveryButtons);
        var hint=Ui.Label("Ferramentas opcionais · consulte THIRD-PARTY.md · recupere para outro disco.",8.5f);hint.ForeColor=Ui.Muted;recovery.Controls.Add(hint);body.Controls.Add(Card("Recuperar e voltar a ter acesso",recovery),0,4);
        log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Color.White;log.ForeColor=Ui.Muted;log.Font=new Font("Segoe UI",9);log.BorderStyle=BorderStyle.None;body.Controls.Add(Card("Atividade recente",log),0,5);
        var footer=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,4,0,0)};activity.Location=new Point(0,0);footer.Controls.Add(activity);status.Location=new Point(29,5);status.Font=new Font("Segoe UI",8.5f);status.ForeColor=Ui.Muted;footer.Controls.Add(status);root.Controls.Add(footer,0,2);root.SetColumnSpan(footer,2);
        entrance.Tick+=(_,_)=>{Opacity=Math.Min(1,Opacity+.09);if(Opacity>=1)entrance.Stop();};
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;MessageBox.Show(this,"Aguarde a conclusão da operação.");}else if(pending.Count>0&&MessageBox.Show(this,"Sair e descartar a fila?","Partition Studio",MessageBoxButtons.YesNo)!=DialogResult.Yes)e.Cancel=true;};
        Shown+=async (_,_)=>{if(!smoke&&Ui.MotionEnabled){Opacity=.12;entrance.Start();}if(smoke)RenderSample();else await LoadDisks();};UpdateQueue();
    }
    protected override void Dispose(bool disposing){if(disposing)entrance.Dispose();base.Dispose(disposing);}
    void Write(string text) {if(log.TextLength>0)log.AppendText(Environment.NewLine);log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}");}
    async Task Work(Func<Task> action)
    {
        if(busy)return; busy=true; SetButtons();
        try {await action();} catch(Exception ex){Write("ERRO: "+ex.Message);MessageBox.Show(this,ex.Message,"Operação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{busy=false;SetButtons();}
    }
    async Task LoadDisks() => await Work(async ()=>{
        status.Text="A ler discos reais…";
        disks=JsonSerializer.Deserialize<Disk[]>(await Backend.Run(new("Inventory")))??[]; Render();
        Write($"Inventário atualizado: {disks.Length} discos. Nenhuma alteração executada.");
    });
    void Render()
    {
        grid.Rows.Clear(); foreach(Control map in maps.Controls.Cast<Control>().ToArray())map.Dispose(); maps.Controls.Clear();
        diskMetric.Value=disks.Length.ToString();diskMetric.Detail="Dispositivos detetados no Windows";
        capacityMetric.Value=Ui.Bytes(disks.Sum(d=>d.Size));capacityMetric.Detail="Soma da capacidade dos discos";
        partitionMetric.Value=disks.Sum(d=>d.Partitions.Length).ToString();partitionMetric.Detail="Volumes e partições especiais";
        diskMetric.Invalidate();capacityMetric.Invalidate();partitionMetric.Invalidate();
        foreach(var d in disks.OrderBy(d=>d.Number)) {
            int i=grid.Rows.Add($"Disco {d.Number} · {d.Name}",d.Style,Ui.Bytes(d.Size),Ui.Bytes(d.LargestFree),d.Protected?"Protegido / indisponível":d.Health);
            grid.Rows[i].Tag=(d,(Part?)null); grid.Rows[i].DefaultCellStyle.Font=new Font(Font,FontStyle.Bold);
            foreach(var p in d.Partitions.OrderBy(p=>p.Offset)) {
                i=grid.Rows.Add("    "+p.Title,p.FileSystem,Ui.Bytes(p.Size),p.FileSystem==""?"—":Ui.Bytes(p.Free),p.Boot||p.System||p.Hidden?"Protegida":p.Type);
                grid.Rows[i].Tag=(d,(Part?)p);
            }
            var map=new DiskMap(d){Width=Math.Max(200,maps.ClientSize.Width-22),Height=103,Margin=new Padding(0,0,0,4)};
            map.PartSelected+=(p)=>{foreach(DataGridViewRow row in grid.Rows)if(row.Tag is ValueTuple<Disk,Part?> x && x.Item1==d && x.Item2==p){grid.CurrentCell=row.Cells[0];break;}};
            maps.Controls.Add(map);
        }
        summary.Text="O seu armazenamento, organizado.";
        status.Text=(Backend.Admin?"Administrador":"Modo de leitura · abra como administrador para gerir discos")+"   |   Disco de sistema protegido   |   v0.3.1-dev";
        SelectRow();
    }
    void SelectRow()
    {
        if(grid.CurrentRow?.Tag is ValueTuple<Disk,Part?> x){selectedDisk=x.Item1;selectedPart=x.Item2;}else{selectedDisk=null;selectedPart=null;}
        foreach(DiskMap map in maps.Controls)map.Selected=selectedPart;
        SetButtons();
    }
    void SetButtons()
    {
        for(int i=0;i<operationButtons.Count;i++) {
            string key=i<5?new[]{"Create","Resize","Format","Delete","Repair"}[i]:"Scan";
            operationButtons[i].Enabled=!busy && Backend.Admin && selectedDisk is not null && Backend.Eligible(selectedDisk,selectedPart,key);
        }
        activity.Active=busy; refresh.Enabled=!busy;advanced.Enabled=!busy; apply.Enabled=!busy&&Backend.Admin&&pending.Count>0; recovery.Enabled=!busy;
    }
    void UpdateQueue(){queue.Items.Clear();foreach(var item in pending)queue.Items.Add(item);apply.Text=$"Aplicar alterações ({pending.Count})";SetButtons();}
    async Task Stage(string action)
    {
        var d=selectedDisk;var p=selectedPart;
        if(d is null || !Backend.Admin || !Backend.Eligible(d,p,action))return;
        if(pending.Any(x=>x.Disk?.Number==d.Number)){MessageBox.Show(this,"Já existe uma operação para este disco. Aplique ou remova-a antes de adicionar outra.");return;}
        long size=0;string fs="NTFS",label="Dados";
        if(action is "Create" or "Resize") {
            long min=16L<<20,max=d.LargestFree;
            if(action=="Resize") {
                bool ok=false;
                await Work(async ()=>{using var limits=JsonDocument.Parse(await Backend.Run(new("Limits",d,p)));min=limits.RootElement.GetProperty("SizeMin").GetInt64();max=limits.RootElement.GetProperty("SizeMax").GetInt64();ok=true;});
                if(!ok)return;
            }
            // Whole MiB boundaries avoid passing rounded human-readable values to Storage.
            long lo=(min+1048575)/1048576, hi=max/1048576;
            if(hi<lo){MessageBox.Show(this,"Não existe um tamanho válido em MiB.");return;}
            using var dialog=new Form{Text=Request.Names[action],Width=470,Height=235,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
            var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown}; dialog.Controls.Add(layout);
            layout.Controls.Add(Ui.Label($"Tamanho em MiB · permitido: {lo:N0} a {hi:N0}"));
            var number=new NumericUpDown{Minimum=lo,Maximum=hi,Value=Math.Clamp((p?.Size??max)/1048576,lo,hi),Width=390,ThousandsSeparator=true};layout.Controls.Add(number);
            var add=Ui.Button("Adicionar à fila",(_,_)=>dialog.DialogResult=DialogResult.OK,true); layout.Controls.Add(add);dialog.AcceptButton=add;
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;size=(long)number.Value*1048576;
        }
        if(action=="Format") {
            using var dialog=new Form{Text="Formatar · apaga os dados do volume",Width=470,Height=280,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
            var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown};dialog.Controls.Add(layout);
            layout.Controls.Add(Ui.Label("Sistema de ficheiros e nome do volume"));
            var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=390};combo.Items.AddRange(["NTFS","exFAT"]);combo.SelectedIndex=0;layout.Controls.Add(combo);
            var name=new TextBox{Text=p?.Label??"Dados",MaxLength=32,Width=390};layout.Controls.Add(name);
            var add=Ui.Button("Adicionar à fila",(_,_)=>dialog.DialogResult=DialogResult.OK,true);layout.Controls.Add(add);dialog.AcceptButton=add;
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;fs=combo.Text;label=name.Text;
        }
        if(action=="Repair" && MessageBox.Show(this,"A reparação coloca o volume temporariamente offline e altera estruturas do sistema de ficheiros. Se pretende recuperar ficheiros perdidos, recupere-os primeiro. Adicionar à fila?","Reparar volume",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        pending.Add(new(action,d,p,size,fs,label));UpdateQueue();Write("Adicionado à fila. Ainda não foi aplicado.");
    }
    async Task Scan()
    {
        var d=selectedDisk;var p=selectedPart;if(d is null||p is null)return;
        await Work(async ()=>{Write("A verificar o sistema de ficheiros…");using var result=JsonDocument.Parse(await Backend.Run(new("Scan",d,p)));Write(result.RootElement.GetProperty("Message").GetString()??"Verificação concluída.");});
    }
    async Task Apply()
    {
        if(pending.Count==0 || !Backend.Admin)return;
        string list=string.Join(Environment.NewLine,pending.Select(x=>x.ToString()));
        using var confirm=new Form{Text="Confirmar alterações reais",Width=670,Height=410,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown};confirm.Controls.Add(layout);
        layout.Controls.Add(Ui.Label("Reveja o destino de cada operação",14,true));
        layout.Controls.Add(new TextBox{Text=list,Multiline=true,ReadOnly=true,Width=610,Height=130,ScrollBars=ScrollBars.Vertical});
        layout.Controls.Add(Ui.Label("Formatar e eliminar apagam dados. Não existe reversão automática.\nConfirme que tem uma cópia dos dados. Escreva APLICAR para continuar.",10));
        var input=new TextBox{Width=610};layout.Controls.Add(input);
        var button=Ui.Button("Aplicar no disco",(_,_)=>{if(input.Text=="APLICAR")confirm.DialogResult=DialogResult.OK;},true);layout.Controls.Add(button);
        if(confirm.ShowDialog(this)!=DialogResult.OK)return;
        await Work(async ()=>{
            while(pending.Count>0) {
                var item=pending[0];Write("A executar: "+item);
                try {
                    using var result=JsonDocument.Parse(await Backend.Run(item));Write(result.RootElement.GetProperty("Message").GetString()??"Concluído.");
                    pending.RemoveAt(0);UpdateQueue();
                } catch {pending.Clear();UpdateQueue();Write("Fila interrompida e descartada. Algumas alterações podem já ter sido aplicadas. Atualize antes de continuar.");throw;}
            }
        });
        await LoadDisks();
    }
    async Task OpenTools()
    {
        if(busy)return;
        if(pending.Count>0){MessageBox.Show(this,"Remova ou aplique a fila antes de abrir ferramentas que podem alterar o estado dos discos.");return;}
        if(disks.Length==0){MessageBox.Show(this,"Atualize o inventário primeiro.");return;}
        busy=true;SetButtons();
        try{using var tools=new ToolsWindow(disks,selectedDisk);tools.ShowDialog(this);}finally{busy=false;SetButtons();}
        await LoadDisks();
    }
    void Elevate()
    {
        if(Backend.Admin){MessageBox.Show(this,"A aplicação já está em modo administrador.");return;}
        try{Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",WorkingDirectory=AppContext.BaseDirectory});}catch(Exception ex){Write(ex.Message);}
    }
    void Export()
    {
        using var save=new SaveFileDialog{Filter="Inventário JSON|*.json",FileName="inventario-discos.json"};
        if(save.ShowDialog(this)==DialogResult.OK)try{File.WriteAllText(save.FileName,JsonSerializer.Serialize(disks,new JsonSerializerOptions{WriteIndented=true}));Write("Inventário exportado.");}catch(Exception ex){MessageBox.Show(this,ex.Message);}
    }
    void Recovery(bool partitions)
    {
        if(pending.Count>0){MessageBox.Show(this,"Remova ou aplique a fila antes de iniciar ferramentas externas. Depois da recuperação, atualize o inventário.");return;}
        string text=partitions?
            "Vai abrir o TestDisk numa janela separada.\n\n1. Confirme o disco pela capacidade e modelo.\n2. Selecione Analyse → Quick Search; use Deeper Search se necessário.\n3. Prima P para listar/copiar ficheiros antes de gravar a tabela.\n4. Write altera a tabela de partições e exige confirmação no TestDisk.\n\nAs proteções do Partition Studio não se aplicam dentro da ferramenta externa. Não grave alterações sem verificar o resultado.":
            "Vai abrir o QPhotoRec numa janela separada.\n\n1. Selecione o disco de origem correto.\n2. Escolha os tipos de ficheiro e a área a pesquisar.\n3. Em Browse, escolha uma pasta noutro disco físico.\n4. Inicie Search e aguarde.\n\nA recuperação por assinaturas pode perder nomes e pastas. Não é garantida a recuperação de todos os ficheiros.";
        if(MessageBox.Show(this,text,partitions?"Recuperação de partições":"Recuperação de ficheiros",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
        string tool=Path.Combine(AppContext.BaseDirectory,"tools","testdisk-7.2",partitions?"testdisk_win.exe":"qphotorec_win.exe");
        if(!File.Exists(tool)){MessageBox.Show(this,"A ferramenta opcional não está instalada. Consulte THIRD-PARTY.md para obter o pacote oficial TestDisk 7.2 e colocá-lo em tools/testdisk-7.2, junto do executável.");return;}
        try{
            var start=new ProcessStartInfo(tool){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(tool)!};
            if(!Backend.Admin)start.Verb="runas";
            Process.Start(start);Write("Ferramenta externa aberta. Confirme o disco na ferramenta. Atualize o inventário quando terminar.");
        }catch(Exception ex){MessageBox.Show(this,ex.Message,"Não foi possível abrir a recuperação");}
    }
    void Help()=>MessageBox.Show(this,"GESTÃO\nSelecione um disco/partição, adicione uma operação à fila e use Aplicar. É necessária elevação. Discos de sistema, partições especiais, volumes encriptados e discos não inicializados não são alterados. Redimensionamento e reparação requerem NTFS.\n\nRECUPERAÇÃO\nPhotoRec recupera conteúdo de ficheiros; TestDisk procura partições perdidas. Abrem como ferramentas externas opcionais. Não há gravação automática de partições. A reparação do volume corrige o sistema de ficheiros; não repara conteúdo interno de documentos corrompidos.\n\nLIMITES\nSem mover/fundir partições ou criar suporte de arranque. Consulte Saúde, backups e clonagem para as novas ferramentas e os seus limites. Não use num disco com avaria física. Consulte LEIA-ME.md para instruções e testes.","Partition Studio · Guia");
    public void RenderSample()
    {
        var p=new Part(1,1048576,180L<<30,"D","Arquivo","NTFS",85L<<30,false,false,false,"Basic","{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}",0);
        disks=[new Disk(1,"Disco de teste — apenas imagem de verificação","sample","sample",256L<<30,"GPT",false,false,false,false,"Healthy",75L<<30,[p])];Render();
        status.Text="VERIFICAÇÃO VISUAL · dados de teste · nenhuma operação de disco";Write("Modo de teste visual.");
    }
}
static class Program
{
    [STAThread] static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Contains("--extended-self-test"))return EngineTests.Run(args.Last());
        if(args.Contains("--diagnose")) {
            try {File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"diagnostico.json"),Task.Run(()=>Backend.Run(new("Inventory"))).GetAwaiter().GetResult());return 0;}
            catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"diagnostico-erro.txt"),ex.ToString());return 1;}
        }
        if(args.Contains("--self-test")) {
            try {
                var p=new Part(1,1048576,1L<<30,"D","Teste","NTFS",1L<<29,false,false,false,"Basic","{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}",0);
                var d=new Disk(1,"Teste","abc","123",2L<<30,"GPT",false,false,false,false,"Healthy",1L<<30,[p]);
                void Assert(bool condition,string name){if(!condition)throw new Exception(name);}
                Assert(Backend.Eligible(d,p,"Resize"),"Resize data");Assert(!Backend.Eligible(d with{Boot=true},p,"Delete"),"Boot protection");
                Assert(!Backend.Eligible(d,p with{Hidden=true},"Format"),"Hidden protection");Assert(!Backend.Eligible(d,p with{FileSystem="exFAT"},"Repair"),"Repair NTFS only");
                Assert(!Backend.Eligible(d with{ReadOnly=true},p,"Create"),"Readonly protection");Assert(!Backend.Eligible(d,p with{GptType="recovery"},"Delete"),"GPT type protection");
                Assert(Backend.Script.Contains("UniqueId")&&Backend.Script.Contains("OfflineScanAndFix"),"Embedded backend");
                var copy=JsonSerializer.Deserialize<Disk[]>(JsonSerializer.Serialize(new[]{d}));Assert(copy![0].Partitions[0]==p,"JSON round trip");
                using var form=new MainWindow(true);form.Show();var settle=Stopwatch.StartNew();while(settle.ElapsedMilliseconds<450){Application.DoEvents();Thread.Sleep(16);}
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(AppContext.BaseDirectory,"verificacao-visual.png"));
                form.Size=form.MinimumSize;Application.DoEvents();
                using var compact=new Bitmap(form.Width,form.Height);form.DrawToBitmap(compact,new Rectangle(0,0,form.Width,form.Height));compact.Save(Path.Combine(AppContext.BaseDirectory,"verificacao-compacta.png"));form.Close();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.txt"),"PASS: 8 verificações; interface renderizada. Nenhuma escrita em discos de dados.\n");return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.txt"),ex.ToString());return 1;}
        }
        Application.Run(new MainWindow());return 0;
    }
}
