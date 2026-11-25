using System.Numerics;
using Raylib_cs;
namespace UnnamedGame;

/// The lighting pipeline: sky -> world albedo -> 2D light map with soft sun shadows -> composite with rim light ->
/// reflective water -> god rays + bloom -> warm grade. Everything happens on a 360-line canvas and is scaled up.
public sealed class Render
{
    const string Head = "#version 330\nin vec2 fragTexCoord; in vec4 fragColor; out vec4 o; uniform sampler2D texture0;\n";
    const string Noise = "float h21(vec2 p){p=fract(p*vec2(123.34,456.21));p+=dot(p,p+45.32);return fract(p.x*p.y);} float vn(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(h21(i),h21(i+vec2(1,0)),f.x),mix(h21(i+vec2(0,1)),h21(i+vec2(1,1)),f.x),f.y);} float fbm(vec2 p){float a=.5,r=0.;for(int i=0;i<4;i++){r+=a*vn(p);p=p*2.03+7.;a*=.5;}return r;}\n";
    const string SkyFs = Head + Noise + @"uniform vec3 top,mid,hor,sun; uniform vec2 size,sunp; uniform float night,t,cloud,golden,rainbow,aurora,sunup; uniform vec3 shoot; uniform vec4 bolt;
void main(){ vec2 uv=vec2(fragTexCoord.x,1.-fragTexCoord.y); float y=uv.y; vec3 c=y<.42?mix(top,mid,smoothstep(0.,.42,y)):mix(mid,hor,smoothstep(.42,.74,y));
 vec2 p=floor(uv*size); float band=h21(p)*.012-.006; c+=band; float clr=1.-smoothstep(.4,.9,cloud);
 if(night>.02){ vec2 g=floor(p/3.); float s=h21(g); if(s>.988){ float tw=.55+.45*sin(t*(1.+s*40.)+s*600.); vec2 f=mod(p,3.); float core=(f.x==1.&&f.y==1.)?1.:((s>.997&&(f.x==1.||f.y==1.))?.45:0.); c+=vec3(.9,.92,1.)*core*tw*night*clr*(1.-smoothstep(.35,.7,y)); }
  float mw=fbm(vec2(uv.x*3.+uv.y*2.,uv.y*5.-uv.x*2.)); c+=vec3(.10,.10,.18)*smoothstep(.45,.8,mw)*night*clr*(1.-y); }
 vec2 d=(uv-sunp)*vec2(size.x/size.y,1.); float r=length(d); float vis=1.-smoothstep(.55,1.,cloud)*.85; vec3 sc=mix(vec3(.9,.93,1.),sun,sunup);
 float R=sunup>.5?.055:.04; float disc=smoothstep(R,R-.006,r); if(sunup<.5){ float cr=vn(d*60.+3.); disc*=.82+.18*cr; float ph=smoothstep(.022,.034,length(d-vec2(.018,-.010))); disc*=mix(.25,1.,ph);}
 c+=sc*exp(-r*(sunup>.5?5.5:9.))*(.18+.26*golden)*vis*(sunup>.5?1.:.5); c=mix(c,mix(sc*1.05+vec3(.12,.1,.06),vec3(.97,.97,.92),1.-sunup),disc*vis*.96); c=mix(c,c.rgb*.45+sun*.1,smoothstep(R+.006,R,r)*(1.-smoothstep(R,R-.006,r))*vis*.5);
 if(aurora>.01){ float b=0.; for(int i=0;i<3;i++){ float yy=.12+.09*float(i)+.05*sin(uv.x*4.+t*.11+float(i)*2.)+.04*vn(vec2(uv.x*7.+t*.07,float(i))); float dd=y-yy; b=dd>0.?exp(-dd*14.):exp(dd*70.); float ray=.6+.4*vn(vec2(uv.x*46.+t*.3,float(i)*9.)); vec3 ac=mix(vec3(.25,.95,.65),vec3(.65,.45,.95),clamp(dd*6.+.3*float(i),0.,1.)); c+=ac*b*ray*aurora*.30; } }
 if(rainbow>.01){ vec2 q=(uv-vec2(1.-sunp.x,1.02))*vec2(size.x/size.y,1.); float rr=length(q); float k=(rr-.62)/.10; if(k>0.&&k<1.){ vec3 rb=clamp(abs(fract(vec3(.0,.667,.333)+(1.-k)*.78)*6.-3.)-1.,0.,1.); c=mix(c,c+rb*.5,min(1.,rainbow)*.6*sin(k*3.14159)); } }
 if(shoot.z<1.1){ vec2 a=shoot.xy+vec2(.22,.10)*shoot.z; vec2 b=a-vec2(.10,.046); vec2 pa=uv-a,ba=b-a; float hh=clamp(dot(pa,ba)/dot(ba,ba),0.,1.); float l=length((pa-ba*hh)*vec2(size.x/size.y,1.)); c+=vec3(1.,.97,.9)*smoothstep(.006,0.,l)*(1.-hh)*(1.-smoothstep(.7,1.1,shoot.z))*night; }
 if(bolt.y<.35){ float x=bolt.x; float e=1.; for(int i=0;i<3;i++){ float yy=floor(y*26.); float jx=x+ (vn(vec2(yy*.5,bolt.z+float(i)*7.))-.5)*.16+(h21(vec2(yy,bolt.z))-.5)*.03; float nx=x+(vn(vec2((yy+1.)*.5,bolt.z+float(i)*7.))-.5)*.16+(h21(vec2(yy+1.,bolt.z))-.5)*.03; float lx=mix(jx,nx,fract(y*26.)); float ww=i==0?.0032:.0015; if(i>0&&y>.45)continue; e=min(e,abs(uv.x-lx)/ww);} float fl=step(.5,fract(bolt.y*14.)+.35); c+=vec3(.9,.92,1.)*(smoothstep(1.,0.,e)*1.4+exp(-e*.12)*.35)*(1.-bolt.y/.35)*fl*step(y,.66); }
 o=vec4(c,1.); }";
    const string HillFs = Head + "uniform vec3 tint,fog; uniform float amt,lit; void main(){ vec4 c=texture(texture0,fragTexCoord); if(c.a<.3) discard; vec3 k=c.rgb*tint; bool w=c.r>.95&&c.g>.8&&c.g<.92&&c.b<.7; if(w) k=mix(k,vec3(1.,.82,.5),lit); o=vec4(mix(k,fog,amt*(w?1.-lit*.8:1.)),1.); }";
    const string LightFs = Head + @"uniform sampler2D tiles; uniform vec2 cam,size,sunDir; uniform vec3 amb,sun; uniform int nl,q; uniform vec4 lp[28]; uniform vec3 lc[28]; uniform float under;
const vec2 WS=vec2(1500.*24.,170.*24.); float solid(vec2 p){ return p.y<0.?0.:texture(tiles,p/WS).g; }
void main(){ vec2 wp=cam+vec2(fragTexCoord.x,1.-fragTexCoord.y)*size; float sky=texture(tiles,wp/WS).r; float self=solid(wp); float occ=0.; int N=q>1?18:9; float st=q>1?20.:40.; float jit=fract(sin(dot(wp,vec2(12.98,78.23)))*43758.5)*.5;
 for(int i=0;i<N;i++){ vec2 p=wp+sunDir*(26.+(float(i)+jit)*st); if(p.y<0.) break; occ+=solid(p)*st*.011; } float sh=exp(-occ*1.5);
 vec3 L=amb*(sky*.86+.04)+sun*sh*smoothstep(.25,.86,sky)*.70*(1.-self*.10)+vec3(.27,.24,.30)*(1.-sky*.82);
 for(int i=0;i<nl;i++){ vec2 d=lp[i].xy-wp; float l=length(d); if(l<lp[i].z){ float a=1.-l/lp[i].z; a=a*a*(1.2-.2*a); if(q>0){ float s=0.; int M=q>1?8:5; for(int k=0;k<M;k++){ s+=solid(wp+d*((float(k)+.5+jit)/float(M)))*step(22.,l*((float(k)+.5)/float(M))); } a*=exp(-s*(q>1?.55:.9)); } L+=lc[i]*a*lp[i].w; } }
 o=vec4(L*.5,1.); }";
    const string CompFs = Head + @"uniform sampler2D light,bg; uniform vec2 size,sunDir; uniform vec3 sun,fogc; uniform float fogA,t,mist,wet; uniform float camx;
" + Noise + @"void main(){ vec2 uv=fragTexCoord; vec4 w=texture(texture0,uv); vec3 l=texture(light,uv).rgb*2.; vec3 b=texture(bg,uv).rgb; vec2 px=1./size; vec2 sd=vec2(sunDir.x,-sunDir.y)*px;
 float e1=1.-texture(texture0,uv+sd*1.6).a, e2=1.-texture(texture0,uv+sd*3.2).a; float rim=w.a*(e1*.75+e2*.25); vec3 c=w.rgb*l; float ls=clamp(dot(l,vec3(.4)),0.,1.4); c+=(w.rgb*.65+.22)*sun*rim*.30*ls;
 float sp=wet*w.a*e1*.14; c+=vec3(.7,.8,.95)*sp*ls; c=mix(b,c,w.a);
 if(mist>.01){ float y=1.-uv.y; float n=fbm(vec2((uv.x*size.x+camx)*.006+t*.03,y*3.2-t*.015)); float m=mist*smoothstep(.15,.95,y+n*.4-.12)*(.35+.65*n); c=mix(c,fogc,m*.58);}
 o=vec4(mix(c,fogc,fogA*w.a),1.); }";
    const string WaterFs = Head + Noise + @"uniform sampler2D light,tiles; uniform vec2 size,cam; uniform float surf,t,rain; uniform vec3 shallow,deep,sun,hor; uniform int kind; uniform vec4 rip[8];
void main(){ vec2 p=vec2(gl_FragCoord.x,size.y-gl_FragCoord.y); vec2 wp=floor(p+cam); float amp=kind==3?2.6:1.2; float wave=sin(wp.x*.045+t*1.5)*amp*.5+sin(wp.x*.11-t*2.1)*amp*.3+sin(wp.x*.021+t*.7)*amp*.4; for(int i=0;i<8;i++){ float d=abs(wp.x-rip[i].x); float a=rip[i].z; if(a>0.) wave+=sin(d*.35-rip[i].y*9.)*a*exp(-d*.03)*exp(-rip[i].y*1.4)*smoothstep(rip[i].y*70.+8.,rip[i].y*70.-30.,d);}
 float s=floor(surf+wave+.5); float dep=wp.y-s; if(dep<0.||texture(tiles,(floor(wp/24.)+.5)/vec2(1500.,170.)).g>.5) discard; vec2 uv=gl_FragCoord.xy/size; float wob=sin(wp.y*.22+t*2.2)*1.1+sin(wp.y*.09-t*1.3+wp.x*.02)*1.5; vec3 l=texture(light,uv).rgb*2.;
 vec3 refr=texture(texture0,uv+vec2(wob*smoothstep(0.,40.,dep),0.)/size).rgb; float ab=1.-exp(-dep*(kind==2?.016:.024)); vec3 wc=mix(shallow,deep,smoothstep(0.,190.,dep)); vec3 c=mix(refr*mix(vec3(1.),shallow*1.35+.22,.75),wc*(l*.75+.14),clamp(.40+ab*.58,0.,.97));
 float ry=s-dep*.82-1.; vec2 ruv=vec2(uv.x+(wob*1.6+ (vn(vec2(wp.x*.06,dep*.35-t*1.6))-.5)*7.)/size.x, 1.-(ry-cam.y)/size.y); float ra=.62*exp(-dep*.020)*step(0.,ruv.y)*step(ruv.y,1.); vec3 refl=texture(texture0,clamp(ruv,0.001,.999)).rgb; c=mix(c,refl*.92+wc*.08,ra);
 float gl=step(.80,vn(vec2(wp.x*.05+t*.25,dep*.5-t*.5)))*exp(-dep*.03); c+=(sun*.55+hor*.12)*gl*.38; float ca=sin(wp.x*.10+sin(wp.y*.08+t)*2.+t*.8)*sin(wp.y*.13-t*.6+sin(wp.x*.05)*2.); c+=shallow*.10*smoothstep(.55,1.,ca)*smoothstep(0.,60.,dep)*exp(-dep*.008)*clamp(l.g,0.,1.5);
 if(dep<1.) c=mix(c,vec3(.97,.98,.96)*(l*.55+.3),.82); else if(dep<2.) c=mix(c,vec3(.8,.92,.95)*(l*.5+.2),.42); else if(dep<4.&&h21(vec2(floor(wp.x/5.),floor(t*2.)))>.72) c+=.12; if(rain>.1){ vec2 g=vec2(floor(wp.x/9.),floor(t*7.+h21(vec2(floor(wp.x/9.),1.))*5.)); if(h21(g)>.8&&dep<3.) c+=.22*rain; }
 o=vec4(c,1.); }";
    const string FallFs = Head + Noise + @"uniform sampler2D light; uniform vec2 size,cam; uniform float t; uniform vec4 rect; void main(){ vec2 p=vec2(gl_FragCoord.x,size.y-gl_FragCoord.y); vec2 wp=floor(p+cam); vec2 q=(wp-rect.xy)/rect.zw; vec2 uv=gl_FragCoord.xy/size; vec3 l=texture(light,uv).rgb*2.; float ed=vn(vec2(wp.y*.05-t*7.,1.))*.10; if(q.x<ed||q.x>1.-ed) discard; float cx=floor(wp.x/2.); float st=vn(vec2(cx*.55,wp.y*.030-t*(3.4+h21(vec2(cx,2.))*1.2))); float st2=vn(vec2(cx*1.3+9.,wp.y*.07-t*6.)); vec3 c=mix(vec3(.45,.72,.84),vec3(.93,.98,1.),smoothstep(.38,.75,st)*.75+st2*.28); c=mix(c,c*.72,smoothstep(.36,.30,st)*.6); float e=min(q.x-ed,1.-ed-q.x)*rect.z; if(e<1.5) c=mix(c,vec3(.25,.42,.55),.75); float foam=smoothstep(.90,1.,q.y+vn(vec2(wp.x*.2,t*5.))*.07); c=mix(c,vec3(1.),foam); o=vec4(c*(l*.72+.22),.93); }";
    const string BrightFs = Head + "uniform sampler2D world; uniform float th; void main(){ vec3 c=texture(texture0,fragTexCoord).rgb; float l=max(c.r,max(c.g,c.b)); o=vec4(c*smoothstep(th,th+.35,l),1.); }";
    const string BlurFs = Head + "uniform vec2 dir; void main(){ vec3 c=texture(texture0,fragTexCoord).rgb*.227; c+=texture(texture0,fragTexCoord+dir*1.385).rgb*.316; c+=texture(texture0,fragTexCoord-dir*1.385).rgb*.316; c+=texture(texture0,fragTexCoord+dir*3.231).rgb*.0702; c+=texture(texture0,fragTexCoord-dir*3.231).rgb*.0702; o=vec4(c,1.); }";
    const string RayFs = Head + @"uniform sampler2D world; uniform vec2 sunp; uniform vec3 col; uniform float asp; void main(){ vec2 uv=fragTexCoord; vec2 d=(sunp-uv)*.92/30.; float a=0.,w=1.; vec2 p=uv; float jit=fract(sin(dot(uv,vec2(12.98,78.23)))*43758.5); p+=d*jit; for(int i=0;i<30;i++){ if(p.x>0.&&p.x<1.&&p.y>0.&&p.y<1.){ float m=1.-texture(world,p).a; vec3 b=texture(texture0,p).rgb; float r=length((p-sunp)*vec2(asp,1.)); a+=m*dot(b,vec3(.33))*exp(-r*2.1)*w; } w*=.965; p+=d; } float rr=length((uv-sunp)*vec2(asp,1.)); o=vec4(col*a*.056*smoothstep(1.7,.1,rr),1.); }";
    const string FinalFs = Head + @"uniform sampler2D bloom,rays; uniform float ba,sat,vig,warm,fade; uniform vec2 size; void main(){ vec2 uv=fragTexCoord; vec3 c=texture(texture0,uv).rgb; c+=texture(rays,uv).rgb; vec3 b=texture(bloom,uv).rgb; c+=b*ba; c=c/(1.+max(vec3(0.),c-.70)*.62); float l=dot(c,vec3(.3,.59,.11)); c=mix(vec3(l),c,sat); c=mix(c,c*vec3(1.05,1.0,.93)+vec3(.012,.004,0.),warm); c=c+(c-c*c)*(c-.5)*.28; vec2 q=uv-.5; c*=1.-dot(q,q)*vig; o=vec4(c*fade,1.); }";
    Shader sky, light, comp, water, fall, bright, blur, ray, fin; public readonly Shader Hill; readonly Dictionary<(uint, string), int> loc = new(); Texture2D tiles; public int W, H = 362; RenderTexture2D bg, world, lt, cA, cB, ray1, half, b1, b2; public RenderTexture2D Out; public float FadeIn = 1;
    public readonly List<(float x, float y, float r, float i, Col c)> Lights = new(); public readonly Vector4[] Ripples = new Vector4[8]; int ripI;
    public void Ripple(float x, float a) { Ripples[ripI] = new(x, 0, a, 0); ripI = (ripI + 1) % 8; }
    public Render(World w)
    {
        Shader L(string fs) { var s = Raylib.LoadShaderFromMemory(null, fs); if (!Raylib.IsShaderValid(s) || s.Id == Rlgl.GetShaderIdDefault()) Console.WriteLine("shader failed: " + fs[200..260]); return s; }
        sky = L(SkyFs); Hill = L(HillFs); light = L(LightFs); comp = L(CompFs); water = L(WaterFs); fall = L(FallFs); bright = L(BrightFs); blur = L(BlurFs); ray = L(RayFs); fin = L(FinalFs);
        var px = new Color[World.W * World.H]; for (int i = 0; i < px.Length; i++) px[i] = new Color((int)(M.Clamp(w.Light[i]) * 255), w.Tile[i] != T.Air ? 255 : 0, 0, 255);
        unsafe { fixed (Color* c = px) { var img = new Image { Data = c, Width = World.W, Height = World.H, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 }; tiles = Raylib.LoadTextureFromImage(img); } }
        Raylib.SetTextureFilter(tiles, TextureFilter.Bilinear); Raylib.SetTextureWrap(tiles, TextureWrap.Clamp);
    }
    int U(Shader s, string n) { if (!loc.TryGetValue((s.Id, n), out int l)) loc[(s.Id, n)] = l = Raylib.GetShaderLocation(s, n); return l; }
    void S(Shader s, string n, float v) => Raylib.SetShaderValue(s, U(s, n), v, ShaderUniformDataType.Float);
    void S(Shader s, string n, int v) => Raylib.SetShaderValue(s, U(s, n), v, ShaderUniformDataType.Int);
    void S(Shader s, string n, Vector2 v) => Raylib.SetShaderValue(s, U(s, n), v, ShaderUniformDataType.Vec2);
    void S(Shader s, string n, Vector3 v) => Raylib.SetShaderValue(s, U(s, n), v, ShaderUniformDataType.Vec3);
    void S(Shader s, string n, Vector4 v) => Raylib.SetShaderValue(s, U(s, n), v, ShaderUniformDataType.Vec4);
    static Vector3 V(Col c) => new(c.R, c.G, c.B);
    RenderTexture2D RT(int w, int h, bool lin) { var r = Raylib.LoadRenderTexture(w, h); Raylib.SetTextureFilter(r.Texture, lin ? TextureFilter.Bilinear : TextureFilter.Point); Raylib.SetTextureWrap(r.Texture, TextureWrap.Clamp); return r; }
    public void Ensure(float vw, float vh)
    {
        int w = (int)MathF.Ceiling(vw) + 2; w += w & 1; int h = (int)MathF.Ceiling(vh) + 2; h += h & 1; if (w == W && h == H && world.Id != 0) return; if (world.Id != 0) foreach (var r in new[] { bg, world, lt, cA, cB, ray1, half, b1, b2, Out }) Raylib.UnloadRenderTexture(r);
        W = w; H = h; bg = RT(w, h, false); world = RT(w, h, false); lt = RT(w / 2, h / 2, true); cA = RT(w, h, false); cB = RT(w, h, false); Out = RT(w, h, false); ray1 = RT(w / 2, h / 2, true); half = RT(w / 2, h / 2, true); b1 = RT(w / 4, h / 4, true); b2 = RT(w / 4, h / 4, true);
    }
    void Pass(RenderTexture2D dst, Shader s, Texture2D src) { Raylib.BeginTextureMode(dst); Raylib.BeginShaderMode(s); Raylib.DrawTexturePro(src, new Rectangle(0, 0, src.Width, -src.Height), new Rectangle(0, 0, dst.Texture.Width, dst.Texture.Height), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); Raylib.EndTextureMode(); }
    static void Copy(RenderTexture2D dst, Texture2D src) { Raylib.BeginTextureMode(dst); Raylib.BeginBlendMode(BlendMode.Alpha); Raylib.DrawTexturePro(src, new Rectangle(0, 0, src.Width, -src.Height), new Rectangle(0, 0, dst.Texture.Width, dst.Texture.Height), Vector2.Zero, 0, Color.White); Raylib.EndBlendMode(); Raylib.EndTextureMode(); }
    void Bind(Shader s, string n, Texture2D t) => Raylib.SetShaderValueTexture(s, U(s, n), t);

    public void Frame(Env e, World wd, float camX, float camY, Action drawBg, Action drawWorld, Action<Vector2> drawShade, Action drawGlow, Action drawOver, float depthUnder)
    {
        int cx = (int)MathF.Floor(camX), cy = (int)MathF.Floor(camY); var size = new Vector2(W, H); var cam = new Vector2(cx, cy); float t = e.T; for (int i = 0; i < 8; i++) Ripples[i].Y += Raylib.GetFrameTime() + .0001f;
        // 1 sky + hills
        S(sky, "top", V(e.Top)); S(sky, "mid", V(e.Mid)); S(sky, "hor", V(e.Hor)); S(sky, "sun", V(e.SunUp ? Col.Lerp(new Col(1, .95f, .8f), e.Sun, .6f) : new Col(.8f, .86f, 1))); S(sky, "size", size); S(sky, "sunp", e.SunScreen); S(sky, "night", e.Night); S(sky, "t", t); S(sky, "cloud", e.Cloud); S(sky, "golden", e.Golden); S(sky, "rainbow", e.Rainbow); S(sky, "aurora", e.Aurora); S(sky, "sunup", e.SunUp ? 1f : 0f); S(sky, "shoot", new Vector3(e.ShootP.X, e.ShootP.Y, e.ShootT)); S(sky, "bolt", new Vector4(e.BoltX, e.BoltT, e.BoltSeed % 100, 0));
        Raylib.BeginTextureMode(bg); Raylib.ClearBackground(Color.Black); Raylib.BeginShaderMode(sky); Raylib.DrawTexturePro(world.Texture, new Rectangle(0, 0, W, -H), new Rectangle(0, 0, W, H), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); drawBg(); Raylib.EndTextureMode();
        // 2 albedo
        Raylib.BeginTextureMode(world); Raylib.ClearBackground(new Color(0, 0, 0, 0)); Rlgl.SetBlendFactorsSeparate(0x0302, 0x0303, 1, 0x0303, 0x8006, 0x8006); Raylib.BeginBlendMode(BlendMode.CustomSeparate); drawWorld(); Raylib.EndBlendMode(); Raylib.EndTextureMode();
        // 3 light map
        S(light, "cam", cam); S(light, "size", size); S(light, "sunDir", e.SunDir); S(light, "amb", V(e.Amb)); S(light, "sun", V(e.Sun)); S(light, "q", Store.Cfg.Light); int n = Math.Min(28, Lights.Count); S(light, "nl", n);
        for (int i = 0; i < n; i++) { var l = Lights[i]; S(light, $"lp[{i}]", new Vector4(l.x, l.y, l.r, l.i)); S(light, $"lc[{i}]", V(l.c)); }
        Raylib.BeginTextureMode(lt); Raylib.BeginShaderMode(light); Bind(light, "tiles", tiles); Raylib.DrawTexturePro(world.Texture, new Rectangle(0, 0, W, -H), new Rectangle(0, 0, W / 2, H / 2), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); Raylib.BeginBlendMode(BlendMode.Multiplied); Rlgl.PushMatrix(); Rlgl.Scalef(.5f, .5f, 1); drawShade(cam); Rlgl.PopMatrix(); Raylib.EndBlendMode(); Raylib.EndTextureMode();
        // 4 composite
        S(comp, "size", size); S(comp, "sunDir", e.SunDir); S(comp, "sun", V(e.Sun)); S(comp, "fogc", V(Col.Lerp(e.Hor, e.Fog, .6f))); S(comp, "fogA", M.Clamp(e.Mist * .16f + e.Rain * .08f) * (1 - depthUnder)); S(comp, "t", t); S(comp, "mist", e.Mist * (1 - depthUnder)); S(comp, "camx", camX); S(comp, "wet", e.Wet);
        Raylib.BeginTextureMode(cA); Raylib.BeginShaderMode(comp); Bind(comp, "light", lt.Texture); Bind(comp, "bg", bg.Texture); Raylib.DrawTexturePro(world.Texture, new Rectangle(0, 0, W, -H), new Rectangle(0, 0, W, H), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); Raylib.EndTextureMode();
        // 5 water
        Copy(cB, cA.Texture);
        Raylib.BeginTextureMode(cB);
        if (wd.Falls.x > 0)
        {
            float fx = wd.Falls.x * 24 + 2, fy = wd.Falls.y0 * 24 + 4, fw = 46, fh = World.LakeY - fy + 4; if (fx + fw > cx && fx < cx + W && fy < cy + H && fy + fh > cy) { S(fall, "size", size); S(fall, "cam", cam); S(fall, "t", t); S(fall, "rect", new Vector4(fx, fy, fw, fh)); Raylib.BeginShaderMode(fall); Bind(fall, "light", lt.Texture); Raylib.DrawTexturePro(cA.Texture, new Rectangle(0, 0, 1, 1), new Rectangle(fx - cx, fy - cy, fw, fh), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); }
        }
        foreach (var p in wd.Pools)
        {
            float x0 = p.X0 * 24 - cx, x1 = (p.X1 + 1) * 24 - cx, y0 = p.Surf - 8 - cy, y1 = (p.Bottom + 1) * 24 - cy; if (x1 < 0 || x0 > W || y1 < 0 || y0 > H) continue;
            Col sc = p.Kind switch { Water.Sea => 0x4fb0b8, Water.Cave => 0x3f9aa8, Water.River => 0x5aa8b0, _ => 0x4f9fb4 }, dc = p.Kind switch { Water.Sea => 0x17486e, Water.Cave => 0x142c48, Water.River => 0x24586a, _ => 0x1c4a6a };
            S(water, "size", size); S(water, "cam", cam); S(water, "surf", p.Surf); S(water, "t", t); S(water, "rain", e.Rain * (1 - depthUnder)); S(water, "shallow", V(sc)); S(water, "deep", V(dc)); S(water, "sun", V(e.Sun)); S(water, "hor", V(e.Hor)); S(water, "kind", (int)p.Kind); for (int i = 0; i < 8; i++) S(water, $"rip[{i}]", Ripples[i]);
            x0 = MathF.Max(x0, -2); x1 = MathF.Min(x1, W + 2); Raylib.BeginShaderMode(water); Bind(water, "light", lt.Texture); Bind(water, "tiles", tiles); Raylib.DrawTexturePro(cA.Texture, new Rectangle(0, 0, 1, 1), new Rectangle(x0, y0, x1 - x0, y1 - y0), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode();
        }
        Raylib.BeginBlendMode(BlendMode.Additive); drawGlow(); Raylib.EndBlendMode(); drawOver(); Raylib.EndTextureMode();
        // 6 rays + bloom
        var sp = e.SunScreen; float rayA = (e.SunUp ? .42f + e.Golden * .9f : .22f) * (1 - M.Smooth(.55f, 1, e.Cloud) * .85f) * (1 + e.Mist * .5f) * (1 - depthUnder); if (Store.Cfg.Light == 0) rayA = 0;
        S(ray, "sunp", new Vector2(sp.X, 1 - sp.Y)); S(ray, "col", V(e.SunUp ? Col.Lerp(new Col(1, .9f, .7f), e.Sun, .7f) : new Col(.5f, .6f, .9f)) * rayA); S(ray, "asp", W / (float)H);
        Raylib.BeginTextureMode(ray1); Raylib.ClearBackground(Color.Black); if (rayA > .01f) { Raylib.BeginShaderMode(ray); Bind(ray, "world", world.Texture); Raylib.DrawTexturePro(bg.Texture, new Rectangle(0, 0, W, -H), new Rectangle(0, 0, W / 2, H / 2), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); } Raylib.EndTextureMode();
        S(bright, "th", .86f); Pass(half, bright, cB.Texture); for (int i = 0; i < 2; i++) { S(blur, "dir", new Vector2((1 + i) / (float)(W / 4), 0)); Pass(b1, blur, i == 0 ? half.Texture : b2.Texture); S(blur, "dir", new Vector2(0, (1 + i) / (float)(H / 4))); Pass(b2, blur, b1.Texture); }
        // 7 grade
        S(fin, "ba", .26f + e.Night * .3f + e.Golden * .06f); S(fin, "sat", 1.06f - e.Storm * .22f); S(fin, "vig", .42f + e.Night * .18f + depthUnder * .25f); S(fin, "warm", .25f + e.Golden * .45f - e.Storm * .25f); S(fin, "fade", FadeIn);
        Raylib.BeginTextureMode(Out); Raylib.BeginShaderMode(fin); Bind(fin, "bloom", b2.Texture); Bind(fin, "rays", ray1.Texture); Raylib.DrawTexturePro(cB.Texture, new Rectangle(0, 0, W, -H), new Rectangle(0, 0, W, H), Vector2.Zero, 0, Color.White); Raylib.EndShaderMode(); Raylib.EndTextureMode();
    }
    /// Blit to the window with sub-pixel camera offset so pixel art scrolls smoothly.
    public void Present(float camX, float camY, float shakeX = 0, float shakeY = 0)
    {
        float fx = camX - MathF.Floor(camX), fy = camY - MathF.Floor(camY); if (Store.Cfg.PixelPerfect) fx = fy = 0; Raylib.DrawTexturePro(Out.Texture, new Rectangle(0, 0, W, -H), new Rectangle(-fx + shakeX, -fy + shakeY, W, H), Vector2.Zero, 0, Color.White);
    }
}
