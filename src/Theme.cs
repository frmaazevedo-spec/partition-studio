using System.Drawing.Drawing2D;

namespace PartitionStudio;

public static class Ui
{
    public static readonly Color Blue=Color.FromArgb(78,105,238), Ink=Color.FromArgb(32,43,65), Muted=Color.FromArgb(119,132,154), Bg=Color.FromArgb(242,245,250);
    public static readonly Color Navy=Color.FromArgb(22,31,52), Teal=Color.FromArgb(27,167,150);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SystemParametersInfoW")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool GetSystemParameter(uint action,uint parameter,[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] out bool value,uint flags);
    static bool SystemMotion(){return GetSystemParameter(0x1042,0,out bool enabled,0)&&enabled;}
    public static bool MotionEnabled=SystemMotion();
    public static string Bytes(long n) => n >= 1L<<40 ? $"{n/(double)(1L<<40):0.##} TiB" : n >= 1L<<30 ? $"{n/(double)(1L<<30):0.##} GiB" : $"{n/(double)(1L<<20):0.##} MiB";
    public static GraphicsPath Round(RectangleF rect,float radius=12)
    {
        float d=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));var p=new GraphicsPath();
        p.AddArc(rect.X,rect.Y,d,d,180,90);p.AddArc(rect.Right-d,rect.Y,d,d,270,90);p.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);p.AddArc(rect.X,rect.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    public static Color Mix(Color a,Color b,float t)=>Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
    public static Button Button(string text,EventHandler click,bool primary=false)
    {
        var b=new MotionButton {Text=text,Primary=primary,IconKey=Key(text),AutoSize=true,Height=40,MinimumSize=new Size(115,40),
            FlatStyle=FlatStyle.Flat,Padding=new Padding(34,5,16,5),Margin=new Padding(0,0,9,8),Cursor=Cursors.Hand,UseVisualStyleBackColor=false};
        b.Click+=click;return b;
    }
    static string Key(string text)=>text.Contains("Saúde")?"shield":text.Contains("Clonar")||text.Contains("imagem")?"disk":text.Contains("Criar")?"plus":text.Contains("Redimension")?"resize":text.Contains("Format")?"format":
        text.Contains("Eliminar")||text.Contains("Remover")?"trash":text.Contains("Repar")?"repair":text.Contains("Verificar")?"scan":
        text.Contains("Atualizar")?"refresh":text.Contains("administrador")?"shield":text.Contains("Exportar")?"export":
        text.Contains("ficheiros")?"file":text.Contains("TestDisk")||text.Contains("partições")?"recover":text.Contains("Guia")?"help":
        text.Contains("Aplicar")||text.Contains("Adicionar")?"check":"arrow";
    public static Label Label(string text,float size=10,bool bold=false)=>new(){Text=text,AutoSize=true,ForeColor=Ink,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,9)};
    public static Bitmap IconBitmap(string key,Color color,int size=24){var b=new Bitmap(size,size);using var g=Graphics.FromImage(b);DrawIcon(g,key,new RectangleF(0,0,size,size),color);return b;}
    public static void DrawIcon(Graphics g,string key,RectangleF bounds,Color color)
    {
        var save=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24,bounds.Height/24);
        using var pen=new Pen(color,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        void Line(float x,float y,float xx,float yy)=>g.DrawLine(pen,x,y,xx,yy);
        void Box(float x,float y,float w,float h,float r=3){using var p=Round(new RectangleF(x,y,w,h),r);g.DrawPath(pen,p);}
        switch(key){
            case "disk":Box(3,4,18,16);Line(3,14,21,14);g.DrawEllipse(pen,6,16.5f,1,1);Line(15,17,18,17);break;
            case "plus":Box(3,3,18,18);Line(12,7,12,17);Line(7,12,17,12);break;
            case "resize":Line(4,12,20,12);Line(4,12,8,8);Line(4,12,8,16);Line(20,12,16,8);Line(20,12,16,16);Line(3,3,3,6);Line(21,3,21,6);Line(3,18,3,21);Line(21,18,21,21);break;
            case "format":Box(4,5,16,15);Line(4,10,20,10);Line(8,3,8,7);Line(16,3,16,7);Line(9,15,15,15);break;
            case "trash":Line(4,6,20,6);Line(9,3,15,3);Line(6,6,7,21);Line(18,6,17,21);Line(7,21,17,21);Line(10,10,10,17);Line(14,10,14,17);break;
            case "repair":g.DrawArc(pen,11,3,10,10,0,260);Line(12,11,3,20);Line(3,20,6,22);Line(6,22,15,13);Line(16,3,15,8);Line(15,8,20,9);break;
            case "scan":g.DrawEllipse(pen,3,3,13,13);Line(15,15,21,21);Line(6,10,9,13);Line(9,13,13,8);break;
            case "refresh":g.DrawArc(pen,4,4,16,16,45,280);Line(20,3,20,9);Line(20,9,14,9);break;
            case "shield":g.DrawPolygon(pen,[new PointF(12,2),new(21,6),new(19,16),new(12,22),new(5,16),new(3,6)]);Line(8,12,11,15);Line(11,15,17,9);break;
            case "export":Box(4,12,16,9);Line(12,3,12,15);Line(12,3,7,8);Line(12,3,17,8);break;
            case "file":Box(5,2,14,20);Line(9,8,15,8);Line(9,12,15,12);Line(9,16,13,16);break;
            case "recover":g.DrawArc(pen,3,3,18,18,30,290);Line(3,3,3,9);Line(3,9,9,9);Line(12,8,12,16);Line(8,12,12,16);Line(16,12,12,16);break;
            case "help":g.DrawEllipse(pen,2,2,20,20);g.DrawArc(pen,8,6,8,7,180,240);Line(12,12,12,14);Line(12,18,12,18.3f);break;
            case "check":Line(5,12,10,17);Line(10,17,20,6);break;
            case "queue":Line(8,6,21,6);Line(8,12,21,12);Line(8,18,17,18);g.DrawEllipse(pen,2,5,1,1);g.DrawEllipse(pen,2,11,1,1);g.DrawEllipse(pen,2,17,1,1);break;
            default:Line(4,12,20,12);Line(20,12,15,7);Line(20,12,15,17);break;
        }
        g.Restore(save);
    }
}

public class MotionButton : Button
{
    public bool Primary {get;set;}
    public bool Dark {get;set;}
    public string IconKey {get;set;}="arrow";
    float hover;bool over,down;
    readonly System.Windows.Forms.Timer animation=new(){Interval=16};
    public MotionButton(){DoubleBuffered=true;animation.Tick+=(_,_)=>{float target=over&&Enabled?1:0;hover+=(target-hover)*.24f;if(Math.Abs(target-hover)<.015f){hover=target;animation.Stop();}Invalidate();};}
    void Animate(){if(Ui.MotionEnabled)animation.Start();else{hover=over?1:0;Invalidate();}}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);over=true;Animate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);over=false;down=false;Animate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);down=true;Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);down=false;Invalidate();}
    protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??Ui.Bg);
        Color normal=Primary?Ui.Blue:Dark?Color.FromArgb(32,44,68):Color.White;
        Color hot=Primary?Color.FromArgb(59,82,210):Dark?Color.FromArgb(43,59,88):Color.FromArgb(235,240,255);
        Color fill=Enabled?Ui.Mix(normal,hot,hover):Dark?Color.FromArgb(27,38,60):Color.FromArgb(235,239,245);
        Color ink=Enabled?(Primary||Dark?Color.White:Ui.Ink):Dark?Color.FromArgb(123,139,163):Color.FromArgb(139,150,170);
        using var shape=Ui.Round(new RectangleF(1,1,Width-3,Height-3),9);using var brush=new SolidBrush(fill);g.FillPath(brush,shape);
        using var border=new Pen(Enabled&&Primary?fill:Dark?Color.FromArgb(49,63,88):Color.FromArgb(222,229,240));g.DrawPath(border,shape);
        int offset=down?1:0;Ui.DrawIcon(g,IconKey,new RectangleF(13,Height/2f-9+offset,18,18),ink);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(39,offset,Math.Max(1,Width-48),Height),ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues){using var focus=new Pen(Ui.Teal,2);using var focusShape=Ui.Round(new RectangleF(4,4,Width-9,Height-9),7);g.DrawPath(focus,focusShape);}
    }
    protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
}

public class Surface : Panel
{
    public Color Fill {get;set;}=Color.White;
    public Surface(){DoubleBuffered=true;Padding=new Padding(16);BackColor=Ui.Bg;}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var shape=Ui.Round(new RectangleF(1,1,Width-3,Height-3),14);using var b=new SolidBrush(Fill);e.Graphics.FillPath(b,shape);using var p=new Pen(Color.FromArgb(225,231,241));e.Graphics.DrawPath(p,shape);}
}
public class Metric : Control
{
    public string Caption {get;set;}="";public string Value {get;set;}="—";public string Detail {get;set;}="";public string IconKey {get;set;}="disk";public Color Accent {get;set;}=Ui.Blue;
    public Metric(){DoubleBuffered=true;Dock=DockStyle.Fill;Margin=new Padding(0,0,12,0);}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Bg);g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=Ui.Round(new RectangleF(1,1,Width-3,Height-3),14);using var fill=new SolidBrush(Color.White);g.FillPath(fill,shape);using var border=new Pen(Color.FromArgb(225,231,241));g.DrawPath(border,shape);
        using var tint=new SolidBrush(Color.FromArgb(18,Accent));g.FillEllipse(tint,Width-60,18,38,38);Ui.DrawIcon(g,IconKey,new RectangleF(Width-51,27,20,20),Accent);
        using var cap=new Font("Segoe UI",9);using var big=new Font("Segoe UI",22,FontStyle.Bold);using var small=new Font("Segoe UI",8.5f);
        TextRenderer.DrawText(g,Caption,cap,new Rectangle(18,13,Width-85,22),Ui.Muted,TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,Value,big,new Rectangle(14,33,Width-75,43),Ui.Ink,TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,Detail,small,new Rectangle(18,80,Width-35,22),Ui.Muted,TextFormatFlags.EndEllipsis);
    }
}
public class ActivityIndicator : Control
{
    readonly System.Windows.Forms.Timer timer=new(){Interval=32};float phase;bool active;
    public bool Active {get=>active;set{active=value;if(value&&Ui.MotionEnabled)timer.Start();else timer.Stop();Invalidate();}}
    public ActivityIndicator(){DoubleBuffered=true;Size=new Size(25,25);timer.Tick+=(_,_)=>{phase=(phase+10)%360;Invalidate();};}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;if(active){using var p=new Pen(Ui.Blue,2.5f){StartCap=LineCap.Round,EndCap=LineCap.Round};e.Graphics.DrawArc(p,5,5,14,14,phase,260);}else{using var b=new SolidBrush(Ui.Teal);e.Graphics.FillEllipse(b,8,8,8,8);}}
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
