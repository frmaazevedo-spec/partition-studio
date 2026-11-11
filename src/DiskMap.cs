using System.Drawing.Drawing2D;

namespace PartitionStudio;

public class DiskMap : Control
{
    readonly Disk disk;
    readonly List<(Rectangle rect,Part? part,long size)> hits=new();
    readonly System.Windows.Forms.Timer animation=new(){Interval=16};
    readonly ToolTip tip=new();
    float reveal,hover;int hovered=-1;Part? selected;
    public event Action<Part?>? PartSelected;
    public Part? Selected {get=>selected;set{selected=value;Invalidate();}}
    public DiskMap(Disk disk)
    {
        this.disk=disk;DoubleBuffered=true;BackColor=Color.White;Cursor=Cursors.Hand;SetStyle(ControlStyles.ResizeRedraw,true);
        reveal=Ui.MotionEnabled?0:1;
        animation.Tick+=(_,_)=>{reveal=Math.Min(1,reveal+.06f);float target=hovered>=0?1:0;hover+=(target-hover)*.23f;if(reveal>=1&&Math.Abs(target-hover)<.02f){hover=target;animation.Stop();}Invalidate();};
        if(Ui.MotionEnabled)animation.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.White);hits.Clear();
        using var bold=new Font("Segoe UI",9,FontStyle.Bold);using var small=new Font("Segoe UI",8.5f);
        Ui.DrawIcon(g,"disk",new RectangleF(1,0,18,18),Ui.Blue);
        TextRenderer.DrawText(g,$"Disco {disk.Number} · {disk.Name}",bold,new Rectangle(26,0,Math.Max(10,Width-205),24),Ui.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g,$"{Ui.Bytes(disk.Size)}  /  {disk.Style}",small,new Rectangle(Math.Max(26,Width-180),1,175,24),Ui.Muted,TextFormatFlags.Right|TextFormatFlags.EndEllipsis);
        int x=1,y=30,w=Width-3;var segments=new List<(long size,Part? part)>();long offset=0;
        foreach(var p in disk.Partitions.OrderBy(p=>p.Offset)){if(p.Offset-offset>2L<<20)segments.Add((p.Offset-offset,null));segments.Add((p.Size,p));offset=p.Offset+p.Size;}
        if(disk.Size-offset>2L<<20)segments.Add((disk.Size-offset,null));
        double weight=segments.Sum(s=>Math.Max(.085,s.size/(double)Math.Max(1,disk.Size)));
        int index=0;
        foreach(var s in segments){int sw=Math.Max(1,(int)(w*Math.Max(.085,s.size/(double)Math.Max(1,disk.Size))/Math.Max(.01,weight)));
            var rect=new Rectangle(x,y,Math.Max(1,sw-5),69);hits.Add((rect,s.part,s.size));
            if(rect.Width>8){
                bool protectedPart=s.part is not null&&(s.part.Boot||s.part.System||s.part.Hidden);
                Color accent=s.part is null?Ui.Muted:protectedPart?Color.FromArgb(154,121,210):Ui.Blue;
                Color fill=s.part is null?Color.FromArgb(245,247,250):protectedPart?Color.FromArgb(246,242,253):Color.FromArgb(239,243,255);
                if(index==hovered)fill=Ui.Mix(fill,Color.FromArgb(218,229,255),hover*.65f);
                using var shape=Ui.Round(rect,8);using var brush=new SolidBrush(fill);g.FillPath(brush,shape);
                using var pen=new Pen(s.part is not null&&s.part==selected?Ui.Blue:Color.FromArgb(225,232,244),s.part is not null&&s.part==selected?1.7f:1);g.DrawPath(pen,shape);
                TextRenderer.DrawText(g,s.part?.Title??"Não alocado",bold,new Rectangle(x+10,y+9,Math.Max(1,rect.Width-19),20),Ui.Ink,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g,$"{Ui.Bytes(s.size)}{(s.part is null?"":" · "+s.part.FileSystem)}",small,new Rectangle(x+10,y+31,Math.Max(1,rect.Width-19),18),Ui.Muted,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
                if(rect.Width>25){
                    var bar=new RectangleF(x+10,y+56,rect.Width-20,4);using var track=new SolidBrush(Color.FromArgb(220,227,240));g.FillRectangle(track,bar);
                    float used=s.part is null?0:s.part.FileSystem==""?1:(float)Math.Clamp(1-s.part.Free/(double)Math.Max(1,s.part.Size),0,1);
                    float progress=Ui.MotionEnabled?1-(float)Math.Pow(1-reveal,3):1;
                    using var ink=new SolidBrush(accent);g.FillRectangle(ink,bar.X,bar.Y,bar.Width*used*progress,bar.Height);
                }
            }
            x+=sw;index++;
        }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);int next=hits.FindIndex(h=>h.rect.Contains(e.Location));if(next==hovered)return;hovered=next;
        if(next>=0){var hit=hits[next];tip.SetToolTip(this,(hit.part?.Title??"Espaço não alocado")+" · "+Ui.Bytes(hit.size)+(hit.part is null?"":$" · Livre: {Ui.Bytes(hit.part.Free)}"));}
        else tip.SetToolTip(this,null);
        if(Ui.MotionEnabled)animation.Start();else{hover=next>=0?1:0;Invalidate();}
    }
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=-1;if(Ui.MotionEnabled)animation.Start();else{hover=0;Invalidate();}}
    protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);foreach(var hit in hits)if(hit.rect.Contains(e.Location)){PartSelected?.Invoke(hit.part);break;}}
    protected override void Dispose(bool disposing){if(disposing){animation.Dispose();tip.Dispose();}base.Dispose(disposing);}
}
