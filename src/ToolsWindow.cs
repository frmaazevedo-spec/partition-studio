using System.Text.Json;

namespace PartitionStudio;

public class ToolsWindow : Form
{
    readonly Disk[] disks;readonly ComboBox source=new(),target=new();readonly TabControl tabs=new();
    readonly DataGridView health=new();readonly TextBox activity=new();readonly ProgressBar progress=new();readonly Button cancel;
    readonly Label selected=Ui.Label(""),stage=Ui.Label("Pronto",9),healthNote=Ui.Label("Atualize para ler os sensores disponíveis.",9);
    readonly ActivityIndicator spinner=new();CancellationTokenSource? cancellation;bool running;
    public ToolsWindow(Disk[] disks,Disk? initial=null,bool preview=false)
    {
        this.disks=disks;Text="Partition Studio · Saúde, manutenção e cópias";Size=new Size(1130,860);MinimumSize=new Size(1050,820);StartPosition=FormStartPosition.CenterParent;BackColor=Ui.Bg;Font=new Font("Segoe UI",10);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),RowCount=5,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,100));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Absolute,90));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(root);
        var header=new Panel{Dock=DockStyle.Fill};var heading=Ui.Label("Proteja os seus discos e os seus dados",21,true);header.Controls.Add(heading);
        source.DropDownStyle=ComboBoxStyle.DropDownList;source.Location=new Point(0,48);source.Width=700;
        foreach(var d in disks)source.Items.Add(new DiskItem(d));if(source.Items.Count>0)source.SelectedIndex=Math.Max(0,Array.FindIndex(disks,d=>d.Number==initial?.Number));header.Controls.Add(source);
        selected.ForeColor=Ui.Muted;selected.Location=new Point(720,53);selected.Text=Backend.Admin?"Administrador":"Leitura · elevação para operações";header.Controls.Add(selected);root.Controls.Add(header,0,0);
        tabs.Dock=DockStyle.Fill;tabs.Padding=new Point(20,10);root.Controls.Add(tabs,0,1);
        var healthPage=Page("Saúde e sensores");
        health.ReadOnly=true;health.AllowUserToAddRows=false;health.AllowUserToDeleteRows=false;health.RowHeadersVisible=false;health.BackgroundColor=Color.White;health.BorderStyle=BorderStyle.None;health.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;health.RowTemplate.Height=29;health.EnableHeadersVisualStyles=false;health.ColumnHeadersDefaultCellStyle.BackColor=Ui.Bg;health.Columns.Add("metric","Indicador");health.Columns.Add("value","Valor reportado");health.DefaultCellStyle.SelectionBackColor=Color.FromArgb(234,239,255);health.DefaultCellStyle.SelectionForeColor=Ui.Ink;
        var healthLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};healthLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));healthLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));healthLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));healthPage.Controls.Add(healthLayout);
        healthLayout.Controls.Add(Ui.Button("Atualizar saúde e temperatura",async (_,_)=>await Run(async token=>{var d=Current;using var json=JsonDocument.Parse(await Backend.Run(new("Health",d),"Advanced.ps1"));ShowHealth(json.RootElement);return "Sensores atualizados. Valores indisponíveis não são substituídos por zero.";})),0,0);
        health.Dock=DockStyle.Fill;healthLayout.Controls.Add(health,0,1);healthNote.Dock=DockStyle.Fill;healthNote.AutoSize=false;healthNote.ForeColor=Ui.Muted;healthLayout.Controls.Add(healthNote,0,2);
        var maintenance=Page("Otimização e setores");var m=Stack(maintenance);
        m.Controls.Add(Ui.Label("Desfragmentação inteligente",15,true));m.Controls.Add(Note("Escolha uma partição NTFS. O Windows decide entre desfragmentação e TRIM conforme o suporte do volume."));
        var volumes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=650};m.Controls.Add(volumes);
        void LoadVolumes(){volumes.Items.Clear();foreach(var p in Current.Partitions.Where(p=>p.FileSystem=="NTFS"&&!string.IsNullOrWhiteSpace(p.Letter)&&!p.Hidden))volumes.Items.Add(new PartItem(p));if(volumes.Items.Count>0)volumes.SelectedIndex=0;}
        source.SelectedIndexChanged+=(_,_)=>LoadVolumes();if(disks.Length>0)LoadVolumes();
        m.Controls.Add(Ui.Button("Otimizar / desfragmentar",async (_,_)=>{
            if(volumes.SelectedItem is not PartItem item)return;
            if(!Confirm($"Otimizar {item.Part.Title} no disco {Current.Number}? O Windows vai alterar a organização do volume.","OTIMIZAR"))return;
            await Run(async token=>Message(await Backend.Run(new("Optimize",Current,item.Part),"Advanced.ps1")),admin:true);
        }));
        m.Controls.Add(Ui.Label("Verificação de setores",15,true));m.Controls.Add(Note("Leitura completa e não destrutiva do disco. Regista endereços de erros de leitura; pode demorar horas. Não força realocação pelo firmware."));
        m.Controls.Add(Ui.Button("Pesquisar setores com erros",async (_,_)=>{
            using var save=new SaveFileDialog{Filter="Relatório JSON|*.json",FileName=$"setores-disco-{Current.Number}.json"};if(save.ShowDialog(this)!=DialogResult.OK)return;
            string reportPath=save.FileName;
            await Run(async token=>{var result=await DiskEngine.Scan(Current,token,Reporter());File.WriteAllText(reportPath,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return $"Pesquisa {(result.Complete?"concluída":"incompleta")}: {result.Issues.Count} setores com erro de leitura. Percorridos {Ui.Bytes(result.Checked)} / {Ui.Bytes(result.Total)}. Relatório: {reportPath}"+(result.Failure is null?"":"\r\nMotivo: "+result.Failure);},admin:true,cancellable:true);
        }));
        m.Controls.Add(Ui.Label("Marcar clusters defeituosos · CHKDSK /r",14,true));m.Controls.Add(Note("Tenta recuperar conteúdo legível e marca clusters defeituosos no NTFS. Altera o volume; recupere primeiro os dados. Sem promessa de reparação física ou realocação. Não se aplica ao disco do Windows em execução."));
        m.Controls.Add(Ui.Button("Verificar e reparar clusters",async (_,_)=>{
            if(volumes.SelectedItem is not PartItem item)return;
            if(!Confirm($"CHKDSK /r em {item.Part.Title} do disco {Current.Number}. Pode demorar horas e alterar estruturas do volume. Feche os programas que o utilizam. Não força desmontagem nem agenda reinício.","REPARAR"))return;
            await Run(async token=>Message(await Backend.Run(new("BadRepair",Current,item.Part),"Advanced.ps1")),admin:true);
        }));
        var backups=Page("Backups e clonagem");var b=Stack(backups);
        b.Controls.Add(Ui.Label("Backup do Windows em execução · VSS",15,true));b.Controls.Add(Note("Cria uma imagem dos volumes críticos do Windows através de wbadmin/VSS, num volume NTFS de outro disco. Não é uma cópia setor a setor. O progresso detalhado só é devolvido no fim."));
        b.Controls.Add(Ui.Button("Criar backup do Windows",async (_,_)=>{
            using var folder=new FolderBrowserDialog{Description="Escolha uma pasta no disco NTFS de destino. O wbadmin usa a raiz desse volume."};if(folder.ShowDialog(this)!=DialogResult.OK)return;
            string path=folder.SelectedPath;string rootPath=Path.GetPathRoot(path)!;
            if(!Confirm($"Criar backup do Windows e volumes críticos em {rootPath}? O wbadmin gere a pasta WindowsImageBackup na raiz. Não formata o destino. Backups anteriores são geridos pelo Windows.","BACKUP"))return;
            await Run(async token=>Message(await Backend.Run(new("WindowsBackup",Path:path),"Advanced.ps1")),admin:true);
        }));
        b.Controls.Add(Ui.Label("Imagem completa e clonagem · disco offline",15,true));b.Controls.Add(Note("Inclui tabela de partições, espaço livre e sistemas operativos existentes. Origem/destino têm de ser discos secundários offline. Clonagem/restauro exigem capacidade exata e setor lógico iguais. A cópia não é redimensionada."));
        var stateButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty};
        stateButtons.Controls.Add(Ui.Button("Colocar origem offline",async (_,_)=>await SetState(true)));stateButtons.Controls.Add(Ui.Button("Colocar origem online",async (_,_)=>await SetState(false)));b.Controls.Add(stateButtons);
        var copyButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty};
        copyButtons.Controls.Add(Ui.Button("Criar imagem do disco",async (_,_)=>{
            using var save=new SaveFileDialog{Filter="Imagem Partition Studio|*.psimg",FileName=$"disco-{Current.Number}.psimg",OverwritePrompt=true};if(save.ShowDialog(this)!=DialogResult.OK)return;
            await Run(token=>DiskEngine.Backup(Current,save.FileName,token,Reporter()),admin:true,cancellable:true);
        }));
        copyButtons.Controls.Add(Ui.Button("Restaurar imagem",async (_,_)=>{
            using var open=new OpenFileDialog{Filter="Imagem Partition Studio|*.psimg"};if(open.ShowDialog(this)!=DialogResult.OK)return;
            if(!Confirm($"RESTAURAR {open.FileName} em {Current.Name} · Disco {Current.Number} · {Ui.Bytes(Current.Size)}. Todo o conteúdo do disco selecionado será substituído. A imagem e o JSON têm de estar juntos.","APAGAR DISCO "+Current.Number))return;
            await Run(token=>DiskEngine.Restore(open.FileName,Current,token,Reporter()),admin:true,cancellable:true);
        }));b.Controls.Add(copyButtons);
        b.Controls.Add(Ui.Label("Disco de destino para clonagem",10,true));target.DropDownStyle=ComboBoxStyle.DropDownList;target.Width=800;foreach(var d in disks)target.Items.Add(new DiskItem(d));if(target.Items.Count>0)target.SelectedIndex=0;b.Controls.Add(target);
        var cloneButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty};
        cloneButtons.Controls.Add(Ui.Button("Colocar destino offline",async (_,_)=>{if(target.SelectedItem is DiskItem t)await SetState(true,t.Disk);}));
        cloneButtons.Controls.Add(Ui.Button("Clonar disco selecionado",async (_,_)=>{
            if(target.SelectedItem is not DiskItem t)return;
            if(!Confirm($"ORIGEM: Disco {Current.Number} · {Current.Name} · {Ui.Bytes(Current.Size)}\nDESTINO: Disco {t.Disk.Number} · {t.Disk.Name} · {Ui.Bytes(t.Disk.Size)}\nTodo o destino será substituído. Ambos têm de estar offline; a origem é lida sem escritas.","APAGAR DISCO "+t.Disk.Number))return;
            await Run(token=>DiskEngine.Clone(Current,t.Disk,token,Reporter()),admin:true,cancellable:true);
        },true));b.Controls.Add(cloneButtons);
        b.Controls.Add(Note("Verificação SHA-256 após cada cópia. Interromper uma clonagem/restauro deixa o destino incompleto. Discos permanecem offline; não existe reversão automática. Não foi validado o arranque de um SO clonado."));
        progress.Dock=DockStyle.Fill;progress.Height=15;progress.Margin=new Padding(0,10,0,10);root.Controls.Add(progress,0,2);
        activity.Multiline=true;activity.ReadOnly=true;activity.ScrollBars=ScrollBars.Vertical;activity.BackColor=Color.White;activity.BorderStyle=BorderStyle.None;activity.Dock=DockStyle.Fill;root.Controls.Add(activity,0,3);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill};footer.Controls.Add(spinner);stage.MaximumSize=new Size(760,40);stage.AutoEllipsis=true;footer.Controls.Add(stage);
        cancel=Ui.Button("Interromper",(_,_)=>cancellation?.Cancel());cancel.Enabled=false;footer.Controls.Add(cancel);root.Controls.Add(footer,0,4);
        FormClosing+=(_,e)=>{if(running){e.Cancel=true;MessageBox.Show(this,"Aguarde a operação ou utilize Interromper quando disponível. Uma cópia interrompida fica incompleta.");}};
        if(preview){health.Rows.Add("Estado Windows","Exemplo visual · não é leitura real");health.Rows.Add("Temperatura","Não disponibilizada (exemplo)");health.Rows.Add("Horas ligado","Não disponibilizadas (exemplo)");health.Rows.Add("Ciclos de alimentação · SMART 12","Não disponibilizados (exemplo)");health.Rows.Add("Setores realocados · SMART 5","Não disponibilizados (exemplo)");healthNote.Text="PRÉ-VISUALIZAÇÃO · dados de teste. Nenhuma operação executada.";}
    }
    Disk Current=>source.SelectedItem is DiskItem x?x.Disk:throw new IOException("Nenhum disco disponível.");
    TabPage Page(string title){var p=new TabPage(title){BackColor=Color.White,Padding=new Padding(18)};tabs.TabPages.Add(p);return p;}
    FlowLayoutPanel Stack(TabPage page){var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.White};page.Controls.Add(p);return p;}
    Label Note(string text){var l=Ui.Label(text,9);l.MaximumSize=new Size(950,0);l.ForeColor=Ui.Muted;l.Margin=new Padding(0,0,0,12);return l;}
    static string Message(string json){using var doc=JsonDocument.Parse(json);return doc.RootElement.GetProperty("Message").GetString()??"Operação concluída.";}
    IProgress<TransferProgress> Reporter()=>new Progress<TransferProgress>(p=>{stage.Text=p.Stage;progress.Style=ProgressBarStyle.Continuous;progress.Value=p.Total>0?(int)Math.Clamp(p.Done*100.0/p.Total,0,100):0;});
    async Task Run(Func<CancellationToken,Task<string>> work,bool admin=false,bool cancellable=false)
    {
        if(running)return;if(admin&&!Backend.Admin){MessageBox.Show(this,"Abra a aplicação principal como administrador e volte a abrir estas ferramentas.");return;}
        running=true;tabs.Enabled=false;source.Enabled=false;cancel.Enabled=cancellable;spinner.Active=true;progress.Style=ProgressBarStyle.Marquee;stage.Text="A executar…";activity.Clear();cancellation=new();
        try{string result=await work(cancellation.Token);if(activity.TextLength>0)activity.AppendText(Environment.NewLine+result);else activity.Text=result;stage.Text="Concluído · consulte o resultado";}
        catch(OperationCanceledException){activity.Text="Operação interrompida. Uma imagem .partial ou um destino parcialmente escrito não é uma cópia válida.";stage.Text="Interrompido";}
        catch(Exception ex){activity.Text="Operação não concluída: "+ex.Message+"\r\nSe a escrita de uma cópia já tinha começado, o destino pode estar incompleto.";stage.Text="Falhou · consulte o resultado";}
        finally{running=false;tabs.Enabled=true;source.Enabled=true;cancel.Enabled=false;spinner.Active=false;progress.Style=ProgressBarStyle.Continuous;cancellation.Dispose();cancellation=null;}
    }
    async Task SetState(bool offline,Disk? chosen=null)
    {
        var d=chosen??Current;if(!Confirm($"{(offline?"Colocar offline":"Colocar online")} Disco {d.Number} · {d.Name}. Offline remove o acesso aos seus volumes. O disco do Windows em execução está protegido.",offline?"OFFLINE":"ONLINE"))return;
        await Run(async token=>Message(await Backend.Run(new(offline?"Offline":"Online",d),"Advanced.ps1")),admin:true);
    }
    bool Confirm(string message,string phrase)
    {
        using var form=new Form{Text="Confirmar operação",Size=new Size(720,350),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Ui.Bg};
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),FlowDirection=FlowDirection.TopDown};form.Controls.Add(layout);
        var text=Ui.Label(message,10);text.MaximumSize=new Size(650,0);layout.Controls.Add(text);layout.Controls.Add(Ui.Label("Escreva: "+phrase,10,true));var input=new TextBox{Width=650};layout.Controls.Add(input);
        layout.Controls.Add(Ui.Button("Confirmar",(_,_)=>{if(input.Text==phrase)form.DialogResult=DialogResult.OK;},true));return form.ShowDialog(this)==DialogResult.OK;
    }
    void ShowHealth(JsonElement data)
    {
        health.Rows.Clear();string Value(string key){return data.TryGetProperty(key,out var value)&&value.ValueKind!=JsonValueKind.Null?value.ToString():"Não disponibilizado";}
        string Smart(string key){return data.GetProperty("Smart").TryGetProperty(key,out var value)?value.ToString():"Não disponibilizado";}
        health.Rows.Add("Estado geral reportado pelo Windows",Value("Health"));health.Rows.Add("Temperatura reportada · °C",Value("Temperature"));health.Rows.Add("Horas ligado reportadas pelo Windows",Value("PowerOnHours"));
        health.Rows.Add("Ciclos de arranque/paragem",Value("StartStopCycles"));health.Rows.Add("Ciclos de carga/descarga",Value("LoadUnloadCycles"));health.Rows.Add("Ciclos de alimentação · SMART ATA 12 (bruto)",Smart("12"));
        health.Rows.Add("Setores realocados · SMART ATA 5 (bruto)",Smart("5"));health.Rows.Add("Setores pendentes · SMART ATA 197 (bruto)",Smart("197"));health.Rows.Add("Setores incorrigíveis · SMART ATA 198 (bruto)",Smart("198"));
        health.Rows.Add("Horas ligado · SMART ATA 9 (bruto)",Smart("9"));health.Rows.Add("Temperatura · SMART ATA 194 (bruto, depende do fabricante)",Smart("194"));
        health.Rows.Add("Desgaste reportado · não é percentagem de saúde",Value("Wear"));health.Rows.Add("Erros de leitura não corrigidos",Value("ReadErrorsUncorrected"));health.Rows.Add("Erros de escrita não corrigidos",Value("WriteErrorsUncorrected"));health.Rows.Add("Previsão de falha SMART",Value("PredictFailure"));
        healthNote.Text="Sensores e SMART dependem do controlador, adaptador USB e fabricante. Um estado Healthy não garante ausência de falhas. Os atributos ATA são valores brutos.";
        activity.Text=string.Join(Environment.NewLine,data.GetProperty("Warnings").EnumerateArray().Select(x=>x.GetString()));
    }
    public void SelectPreviewTab(int index)=>tabs.SelectedIndex=index;
    record DiskItem(Disk Disk){public override string ToString()=>$"Disco {Disk.Number} · {Disk.Name} · {Ui.Bytes(Disk.Size)}{(Disk.Offline?" · offline":"")}{(Disk.Boot||Disk.System?" · Windows em execução":"")}";}
    record PartItem(Part Part){public override string ToString()=>Part.Title+" · "+Ui.Bytes(Part.Size);}
}
