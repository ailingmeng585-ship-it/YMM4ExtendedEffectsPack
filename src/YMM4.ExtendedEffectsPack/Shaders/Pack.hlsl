// Original algorithms; no CapCut/Adobe assets or shaders are redistributed.
// Direct2D complex-input pixel shader ABI. TEXCOORD.xy is the current texture
// coordinate and TEXCOORD.zw is texture pixels per scene DIP, including tiling.
Texture2D Image0 : register(t0);
Texture2D Image1 : register(t1);
SamplerState Sampler0 : register(s0);
SamplerState Sampler1 : register(s1);
cbuffer Constants : register(b0)
{
    float4 Rect;       // left, top, width, height in local DIPs
    float4 Meta;       // effect ID, seconds (integrated cycles for heartbeat), progress, transition
    float4 A;
    float4 B;
    float4 C;
    float4 Tint;       // straight RGBA from the color picker
    float4 History;    // x: a real delayed frame is available
};
static float2 scene;
static float4 tex0, tex1;
static const float PI = 3.14159265359;

float4 sample0(float2 uv)
{
    if (any(uv < 0) || any(uv > 1)) return 0;
    float2 target = Rect.xy + uv * Rect.zw;
    return Image0.SampleLevel(Sampler0, tex0.xy + (target - scene) * tex0.zw, 0);
}
float4 sample1(float2 uv)
{
    if (any(uv < 0) || any(uv > 1)) return 0;
    float2 target = Rect.xy + uv * Rect.zw;
    return Image1.SampleLevel(Sampler1, tex1.xy + (target - scene) * tex1.zw, 0);
}
float4 source(float2 uv)
{
    return Meta.w > 0.5 && Meta.z >= 0.5 ? sample1(uv) : sample0(uv);
}
float3 straight(float4 c) { return c.a > 0.00001 ? c.rgb / c.a : 0; }
float luminance(float4 c) { return dot(straight(c), float3(0.299, 0.587, 0.114)); }
float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float noise(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
    return lerp(lerp(hash(i), hash(i+float2(1,0)), f.x),
                lerp(hash(i+float2(0,1)), hash(i+1), f.x), f.y);
}
float fbm(float2 p)
{
    float r=0, a=0.533333;
    [unroll] for (int i=0;i<4;i++) { r+=noise(p)*a; p=p*2.03+17.1; a*=0.5; }
    return r;
}
float3 rainbow(float h) { return saturate(abs(frac(h+float3(0,2.0/3,1.0/3))*6-3)-1); }
float edge(float2 uv, float radius)
{
    float2 d=max(1,radius)/Rect.zw;
    float4 l=source(uv-float2(d.x,0)), r=source(uv+float2(d.x,0));
    float4 t=source(uv-float2(0,d.y)), b=source(uv+float2(0,d.y));
    return saturate(length(float2(l.a-r.a,t.a-b.a))+
        length(float2(luminance(l)-luminance(r),luminance(t)-luminance(b))));
}
float4 glow(float4 c, float3 color, float amount)
{
    // Preserve premultiplied alpha: translucent edges do not become black/opaque.
    return float4(saturate(c.rgb + color * amount * c.a),c.a);
}
float4 blur(float2 uv, float radius)
{
    float2 d=radius/Rect.zw;
    float4 sum=source(uv)*4;
    sum+=source(uv+float2(d.x,0))+source(uv-float2(d.x,0));
    sum+=source(uv+float2(0,d.y))+source(uv-float2(0,d.y));
    sum+=source(uv+d)+source(uv-d)+source(uv+float2(d.x,-d.y))+source(uv+float2(-d.x,d.y));
    return sum/12;
}
float4 rgbSplit(float2 uv, float2 delta)
{
    float4 r=source(uv+delta), g=source(uv), b=source(uv-delta);
    float alpha=max(r.a,max(g.a,b.a));
    return float4(r.r,g.g,b.b,alpha);
}
float openProgress(float manual) { return Meta.w > 0.5 ? abs(2*Meta.z-1) : saturate(manual/100); }

float4 curtain(float2 uv)
{
    float p=openProgress(A.x);
    if (p>=1) return source(uv);
    float width=max(0.0001,1-p);
    float x=A.y<0.5 ? min(uv.x,1-uv.x)*2 : (A.y<1.5 ? uv.x : 1-uv.x);
    if (x>width) return source(uv);
    float local=x/width;
    float fold=sin(local*A.z*PI*2 + 0.18*sin(uv.y*4));
    float shade=0.45+0.4*(fold*0.5+0.5);
    float spec=pow(saturate(fold),12)*0.35*A.w;
    float3 color=Tint.rgb*shade+spec;
    float hem=0.957+0.008*cos(local*A.z*PI*2);
    if (B.x>0.5 && uv.y>hem)
        color=float3(0.85,0.66,0.18)*(0.65+0.35*pow(abs(sin(local*210)),0.3));
    return float4(color*Tint.a,Tint.a);
}
float4 blink(float2 uv)
{
    float open=openProgress(A.x);
    if (Meta.w<0.5 && A.y>0.5)
    {
        float phase=frac(Meta.y*A.z/60);
        float closing=smoothstep(0.0,0.045,phase)*(1-smoothstep(0.055,0.13,phase));
        open=1-closing;
    }
    if (open>=0.99999) return source(uv);
    if (open<=0.00001) return float4(Tint.rgb*Tint.a,Tint.a);
    float2 p=uv*2-1;
    // Fully closed at 0, fully open at 1, including the corners (the draft reversed these).
    float aperture=open*(1+0.35*(1-p.x*p.x));
    float mask=1-smoothstep(aperture-2/Rect.w,aperture+2/Rect.w,abs(p.y));
    float4 c=blur(uv,A.w*(1-open)); c.rgb*=0.3+0.7*open;
    return lerp(float4(Tint.rgb*Tint.a,Tint.a),c,mask);
}
float3 palette16(float3 color)
{
    // Exactly 16 entries, not RGB 4x4x4 (which would incorrectly yield 64).
    const float3 colors[16]={float3(0,0,0),float3(.33,0,.5),float3(.1,.15,.6),float3(0,.5,.55),
      float3(.6,.1,.15),float3(.6,.25,0),float3(.55,.4,.25),float3(.5,.5,.5),
      float3(.25,.25,.25),float3(.2,.6,.9),float3(.3,.7,.2),float3(.3,.9,.8),
      float3(.95,.3,.4),float3(.8,.5,.85),float3(1,.85,.35),float3(1,1,1)};
    float dist=100; float3 result=0;
    [unroll] for(int i=0;i<16;i++) { float d=dot(color-colors[i],color-colors[i]); if(d<dist){dist=d; result=colors[i];} }
    return result;
}
float4 pixelate(float2 uv)
{
    float2 size=max(float2(1,1),A.xy), pixel=uv*Rect.zw;
    float2 center=(floor(pixel/size)+0.5)*size;
    float2 local=(pixel-center)/size;
    if(A.z>1.5)
    {
        // Nearest of two staggered triangular lattices gives a true hexagonal Voronoi grid.
        float2 q=pixel/size, period=float2(1.7320508,3);
        float2 h0=floor(q/period+0.5)*period;
        float2 h1=floor((q-float2(.8660254,1.5))/period+0.5)*period+float2(.8660254,1.5);
        float2 h=dot(q-h0,q-h0)<dot(q-h1,q-h1) ? h0:h1;
        local=q-h; center=h*size;
    }
    float4 c=source(saturate(center/Rect.zw));
    float3 color=straight(c);
    float dither=B.z>0.5 ? (frac(dot(floor(pixel),float2(.5,.25)))-.375)/8 : 0;
    if(A.w>0.5 && A.w<1.5) color=palette16(saturate(color+dither));
    else if(A.w<2.5 && A.w>1.5) color=floor(saturate(color+dither)*3+.5)/3;
    else if(A.w>2.5)
    {
        float lum=dot(color,float3(.299,.587,.114))+dither;
        const float3 gb[4]={float3(.06,.22,.06),float3(.19,.38,.19),float3(.55,.67,.06),float3(.61,.74,.06)};
        color=gb[(int)clamp(floor(lum*4),0,3)];
    }
    float gap=A.z>1.5 ? max(abs(local.x)*.8660254+abs(local.y)*.5,abs(local.y)) : max(abs(local.x),abs(local.y));
    if(A.z>0.5 && A.z<1.5 && length(local)>.46) color=Tint.rgb;
    if(B.x>.5 && gap>(A.z>1.5 ? .82:.46)) color=lerp(color,Tint.rgb,B.y*Tint.a);
    return float4(color*c.a,c.a);
}
float4 heartbeat(float2 uv)
{
    float t=frac(Meta.y), beat=0;
    if(A.x>.5) beat=.5-.5*cos(t*2*PI);
    else if(t<.15) beat=sin(t/.15*PI);
    else if(t>=.2 && t<.45) beat=.7*sin((t-.2)/.25*PI);
    float2 q=(uv-.5)/(1+beat*A.z/100)+.5;
    float4 c=rgbSplit(q,float2(B.x*beat/Rect.z,0));
    float vignette=saturate(length((uv-.5)*1.414));
    c.rgb=lerp(c.rgb,Tint.rgb*c.a,vignette*beat*A.w*Tint.a);
    return c;
}
float4 gaming(float2 uv)
{
    float angle=A.z*PI/180;
    float h=Meta.y*A.y*.2+(A.x>.5 ? dot(uv,float2(cos(angle),sin(angle)))*2:0);
    float3 rgb=rainbow(h); float4 c=source(uv);
    if(A.x>1.5) return glow(c,rgb,edge(uv,2)*A.w+edge(uv,6)*A.w*.3);
    float3 s=straight(c), light=rgb*A.w, result;
    if(B.x<.5) result=lerp(s,s*light,.8);
    else if(B.x<1.5) result=s+light*.35;
    else if(B.x<2.5) result=1-(1-s)*(1-saturate(light*.5));
    else result=rgb*max(s.r,max(s.g,s.b));
    return float4(saturate(result)*c.a,c.a);
}
float4 blockWave(float2 uv)
{
    float count=max(5,A.x); float2 grid=floor(uv*count), center=(grid+.5)/count;
    float dist=A.y>1.5 ? length(center-.5) : (A.y>.5 ? center.y:center.x);
    float wave=sin(dist*10-Meta.y*B.x);
    if(A.z>.5) wave=floor(wave*4)/4;
    float env=Meta.w>.5 ? sin(PI*Meta.z):1;
    float2 direction=A.y>1.5 ? normalize(center-.5+0.0001) : (A.y>.5 ? float2(1,0):float2(0,1));
    float2 q=uv+direction*wave*A.w/Rect.zw*env;
    float4 c;
    if(Meta.w>.5)
    {
        float mask=smoothstep(center.x*.7,center.x*.7+.3,Meta.z);
        c=lerp(sample0(q),sample1(q),mask);
    }
    else c=source(q)*saturate(B.z/100);
    float2 border=abs(frac(uv*count)-.5);
    if(B.y>.5) c.rgb*=lerp(1,.6,step(.46,max(border.x,border.y))*env);
    return c;
}
float4 flame(float2 uv)
{
    float2 p=uv*float2(3,6)+float2(0,Meta.y*A.y);
    float n=fbm(p), n2=fbm(p*2+1.5);
    // Sampling lower pixels makes the visible image travel upward.
    float2 q=uv+float2(n-.5,abs(n2)*.5)*A.x/100*(1-uv.y)*.35;
    float4 c=source(q);
    if(A.z>.5)
    {
        float l=luminance(c);
        float3 dark=A.w<.5 ? float3(.5,.01,0) : (A.w<1.5 ? float3(0,.05,.7):float3(.3,0,.6));
        float3 mid=A.w<.5 ? float3(1,.35,.01) : (A.w<1.5 ? float3(.05,.6,1):float3(.8,.15,1));
        float3 col=lerp(dark,mid,saturate(l*2)); col=lerp(col,float3(1,1,.8),saturate((l-.5)*2));
        c.rgb=lerp(c.rgb,col*c.a,B.z);
    }
    float dissolve=B.y/100;
    float coverage=dissolve<=0 ? 1 : (dissolve>=1 ? 0:smoothstep(dissolve-.05,dissolve+.05,uv.y*.8+n*.2));
    c*=coverage;
    if(B.x>.5)
    {
        float2 cell=floor(float2(uv.x*90,uv.y*60+Meta.y*A.y*8));
        float spark=step(.988,hash(cell))*pow(saturate(n*1.5),4);
        c=glow(c,float3(1,.65,.15),spark);
    }
    return c;
}
float4 godRays(float2 uv)
{
    float2 delta=uv-A.xy;
    float angle=atan2(delta.y,delta.x);
    float pattern=pow(.5+.5*sin(angle*A.z*2),lerp(14,1,B.y));
    pattern*=lerp(1,fbm(float2(angle*12,Meta.y*.15))*1.8,B.x);
    float illum=0, decay=1;
    [unroll] for(int i=0;i<24;i++)
    {
        float2 q=uv-delta*(float(i)/24)*A.w;
        float4 c=source(q);
        illum+=luminance(c)*c.a*decay;
        decay*=.94;
    }
    float4 c=source(uv);
    return glow(c,Tint.rgb,pattern*(.12+illum/12)*Tint.a);
}
float glyph(float2 cell, float2 local, int charset)
{
    uint digit=(uint)floor(hash(cell)*10);
    // Original 4x7 bitmap glyphs: legible digits, A/F and simple katakana forms.
    const uint digits[10]={0x6999996,0x2222262,0xF248196,0x6996196,0x88F9AA8,0x698717F,0x6997176,0x222488F,0x6996996,0x698E996};
    const uint kana[6]={0x22248AF,0x222222F,0x2488AFA,0x1248888,0x248888F,0x1248AAA};
    uint bits=digits[charset==2 ? digit%2:digit];
    if(charset==1 && hash(cell+7)>.4) bits=kana[digit%6];
    if(charset==0 && digit>7) bits=digit==8 ? 0x999F996 : 0x111711F;
    int2 p=(int2)floor(local*float2(5,8));
    if(p.x>=4 || p.y>=7) return 0;
    return (bits>>(p.y*4+p.x))&1;
}
float4 matrixRain(float2 uv)
{
    float size=max(8,A.z); float2 cells=Rect.zw/size, grid=floor(uv*cells);
    float rowCount=max(1,ceil(cells.y));
    float head=frac(Meta.y*A.y*.1+hash(float2(grid.x,1)))*rowCount;
    float distance=frac((head-grid.y)/rowCount)*rowCount;
    float light=distance<1 ? 1.6:saturate(1-distance/max(1,B.x));
    float ink=glyph(float2(grid.x,grid.y+floor(Meta.y*A.y*3)),frac(uv*cells),(int)A.w);
    float4 c=source(uv);
    float3 rgb=Tint.rgb*ink*light;
    if(A.x>.5) c.rgb=rgb*luminance(source((grid+.5)/cells))*c.a;
    else c=glow(c,rgb,Tint.a);
    if(Meta.w>.5)
    {
        float threshold=frac(hash(float2(grid.x,0))+grid.y/rowCount);
        float mask=smoothstep(threshold-.06,threshold+.06,Meta.z);
        c=lerp(sample0(uv),sample1(uv),mask);
        c=glow(c,rgb,sin(PI*Meta.z));
    }
    else c*=saturate(B.y/100);
    return c;
}
float4 thunder(float2 uv)
{
    float t=floor(Meta.y*A.y);
    float4 c=source(uv);
    if(A.x>.5)
    {
        float sparks=step(.55,fbm(uv*25+t))*edge(uv,A.z);
        return glow(c,Tint.rgb,sparks*2*Tint.a);
    }
    float path=.5+(fbm(float2(uv.y*A.w,t))-.5)*.45;
    float width=max(1,A.z)/Rect.z;
    float d=abs(uv.x-path), bolt=exp(-d/width)+.4*exp(-d/(width*5));
    // Smaller branching discharge from the central path.
    float branch=path+max(0,uv.y-.4)*.5;
    bolt+=exp(-abs(uv.x-branch)/(width*.5))*step(.4,uv.y)*.6;
    float flash=step(.83,hash(float2(t,5)))*B.x;
    return glow(c,Tint.rgb,bolt*Tint.a)+float4(flash*c.a,flash*c.a,flash*c.a,0);
}
float4 shake(float2 uv)
{
    float env=exp(-Meta.y/max(.1,B.x)*5);
    float2 jitter=float2(sin(Meta.y*A.z*2*PI),sin(Meta.y*A.z*2*PI*1.37+1))*A.x/Rect.zw*env;
    float2 q=(uv-.5)/(1+A.w/100*env)+.5+jitter;
    float4 sum=0;
    [unroll] for(int i=0;i<12;i++) sum+=source(saturate(q+(q-.5)*(float(i)/11-.5)*A.y*env*.25));
    return sum/12;
}
float4 filmBurn(float2 uv)
{
    float p=Meta.w>.5 ? Meta.z:A.x/100;
    if(p<=0 || p>=1) return source(uv);
    float sweep=p*2-.5;
    float n=fbm(uv*4+Meta.y*.2);
    float light=exp(-abs(uv.x-sweep+(n-.5)*.4)/max(.05,A.z*.23))*sin(PI*p);
    if(Meta.w>.5) light=max(light,pow(sin(PI*p),18));
    float3 color=A.y<.5 ? float3(1,.24,.035):(A.y<1.5 ? rainbow(uv.y*.6+p)*.7+.3:float3(.05,.75,1));
    float4 c=source(uv);
    float3 rgb=straight(c);
    if(B.x>.5) rgb*=1-light*.7;
    rgb=lerp(rgb,color,saturate(light*1.4));
    rgb=lerp(rgb,1,saturate((light-.55)*2.3));
    if(A.w>.5) rgb+=(hash(floor(uv*Rect.zw)+floor(Meta.y*24))-.5)*.06*sin(PI*p);
    return float4(saturate(rgb)*c.a,c.a);
}
float4 motionGhost(float2 uv)
{
    float4 c=source(uv);
    if(History.x<.5 || A.y<=0) return c;
    float2 q=(uv-.5)/(1+A.w/100)+.5, delta=float2(A.z/Rect.z,0);
    float4 r=sample1(q+delta), g=sample1(q), b=sample1(q-delta);
    float alpha=max(r.a,max(g.a,b.a));
    float weight=A.y*exp(-B.x*A.x/30);
    float4 ghost=float4(r.r,g.g,b.b,alpha)*weight;
    // Screen-ish echo also remains visible on opaque video, with valid premultiplied output.
    float outAlpha=c.a+ghost.a*(1-c.a);
    return float4(min(outAlpha,c.rgb+ghost.rgb*(1-c.rgb)),outAlpha);
}
float4 vhs(float2 uv)
{
    float t=floor(Meta.y*30), row=floor(uv.y*Rect.w/8);
    float burst=step(1-A.w,hash(float2(t,row)));
    float shift=(hash(float2(row,t))-.5)*.08*A.x*burst;
    float tracking=A.z>.5 ? smoothstep(.85,1,uv.y)*sin(uv.y*150+t)*A.x*.04:0;
    float4 c=rgbSplit(float2(frac(uv.x+shift+tracking),uv.y),float2(A.x*4/Rect.z,0));
    float lines=1-A.y*(.5+.5*sin(uv.y*Rect.w*PI))*.45;
    c.rgb*=lines;
    if(B.x>.5) c.rgb=lerp(c.rgb,float3(luminance(c),luminance(c),luminance(c))*c.a,.35)*.9;
    c.rgb+= (hash(floor(uv*Rect.zw)+t)-.5)*A.x*.15*c.a;
    return float4(clamp(c.rgb,0,c.a),c.a);
}
float4 inkBleed(float2 uv)
{
    float p=Meta.w>.5 ? Meta.z:A.x/100;
    float2 center=B.xy;
    float d=length((uv-center)*float2(Rect.z/Rect.w,1));
    float maxD=length(float2(max(center.x,1-center.x)*Rect.z/Rect.w,max(center.y,1-center.y)));
    float field=saturate(d/max(.001,maxD)*.68+fbm(uv*A.y+A.w)*.32);
    float mask=p<=0 ? 0:(p>=1 ? 1:1-smoothstep(p-A.z,p+A.z,field));
    float4 c=Meta.w>.5 ? lerp(sample0(uv),sample1(uv),mask):sample0(uv)*mask;
    float rim=(1-smoothstep(0,A.z*.6,abs(field-p)))*sin(PI*p);
    c.rgb=lerp(c.rgb,Tint.rgb*c.a,rim*.7*Tint.a);
    return c;
}
float4 lumaMelt(float2 uv)
{
    float p=Meta.w>.5 ? Meta.z:A.x/100;
    float lum=luminance(sample0(uv));
    float threshold=A.w>.5 ? 1-lum:lum;
    float mask=p<=0 ? 0:(p>=1 ? 1:1-smoothstep(p-A.y,p+A.y,threshold));
    float2 q=uv+float2(0,sin(p*PI)*A.z/Rect.w*(1-lum));
    return Meta.w>.5 ? lerp(sample0(q),sample1(q),mask):sample0(q)*mask;
}
float2 mirrored(float2 uv) { return 1-abs(frac(uv*.5)*2-1); }
float4 prismWhip(float2 uv)
{
    float p=Meta.w>.5 ? Meta.z:A.x/100, env=sin(PI*p);
    float2 direction=float2(cos(A.y*PI/180),sin(A.y*PI/180));
    float2 q=(uv-.5)/(1+B.x/100*env)+.5;
    float2 split=direction*A.w/Rect.zw*env;
    float4 sum=0;
    [unroll] for(int i=0;i<12;i++)
    {
        float2 offset=direction*(float(i)/11-.5)*A.z*env*.3;
        if(Meta.w>.5)
        {
            float2 u=mirrored(q+direction*p+offset), v=mirrored(q+direction*(p-1)+offset);
            float4 f=sample0(u), s=sample1(v);
            f.r=sample0(mirrored(u+split)).r; f.b=sample0(mirrored(u-split)).b;
            s.r=sample1(mirrored(v+split)).r; s.b=sample1(mirrored(v-split)).b;
            sum+=lerp(f,s,smoothstep(.35,.65,p));
        }
        else sum+=rgbSplit(mirrored(q+direction*env*.1+offset),split);
    }
    return sum/12;
}
float4 main(float4 position:SV_POSITION, float4 scenePosition:SCENE_POSITION,
            float4 uv0:TEXCOORD0, float4 uv1:TEXCOORD1):SV_Target
{
    scene=scenePosition.xy; tex0=uv0; tex1=uv1;
    float2 uv=(scene-Rect.xy)/max(Rect.zw,float2(1,1));
    if(Meta.w>.5 && Meta.z<=0) return sample0(uv);
    if(Meta.w>.5 && Meta.z>=1) return sample1(uv);
    float4 result=0;
    [branch] switch((int)Meta.x)
    {
        case 0: result=curtain(uv); break;
        case 1: result=blink(uv); break;
        case 2: result=pixelate(uv); break;
        case 3: result=heartbeat(uv); break;
        case 4: result=gaming(uv); break;
        case 5: result=blockWave(uv); break;
        case 6: result=flame(uv); break;
        case 7: result=godRays(uv); break;
        case 8: result=matrixRain(uv); break;
        case 9: result=thunder(uv); break;
        case 10: result=shake(uv); break;
        case 11: result=filmBurn(uv); break;
        case 12: result=motionGhost(uv); break;
        case 13: result=vhs(uv); break;
        case 14: result=inkBleed(uv); break;
        case 15: result=lumaMelt(uv); break;
        case 16: result=prismWhip(uv); break;
        default: result=source(uv); break;
    }
    result.a=saturate(result.a);
    result.rgb=clamp(result.rgb,0,result.a);
    return result;
}
