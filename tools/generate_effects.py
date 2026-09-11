#!/usr/bin/env python3
"""Single source of truth for typed YMM4 controls, enum presets and shader slots.
Run from any directory. Generated C# is committed; Python is not needed to build.
"""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'src/YMM4.ExtendedEffectsPack/Effects'

def n(key, label, default, low, high, unit=''):
    return dict(key=key, label=label, type='number', default=default, low=low, high=high, unit=unit)
def e(key, label, options, default=0):
    return dict(key=key, label=label, type='enum', options=options.split('|'), default=default)
def b(key, label, default=True):
    return dict(key=key, label=label, type='bool', default=default)
def c(label, rgb):
    return dict(key='Tint', label=label, type='color', default=rgb)
def preset(name, **values):
    return dict(name=name, values=values)

CATALOG = [
 dict(key='TheaterCurtain', name='劇場カーテン', transition=True, params=[
  n('Progress','進行度',0,0,100,'%'), e('Opening','開き方','中央両開き|左へ片引き|右へ片引き'),
  n('Folds','ヒダの数',12,4,30), b('Gloss','ベルベット光沢'), b('Fringe','金フリンジ'), c('カーテン色',[138,11,26])], presets=[
  preset('クラシック深紅'), preset('エレガントゴールド',Tint=[202,157,46],Folds=18), preset('モダンブラック',Tint=[20,20,24],Gloss=False,Fringe=False), preset('ロイヤルブルー',Tint=[25,40,145])]),
 dict(key='Blink', name='まばたき', transition=True, params=[
  n('Progress','開き具合',100,0,100,'%'), b('AutoLoop','自動ループ',False), n('Frequency','まばたき頻度',18,5,60,'回/分'), n('Blur','ピンボケ強度',8,0,20,'px'), c('まぶたの色',[0,0,0])], presets=[
  preset('目覚め',Progress=0,Blur=18), preset('通常まばたき',Blur=4), preset('気絶・意識消失',Progress=10,Blur=20), preset('自動パチパチ',AutoLoop=True,Frequency=18,Blur=2)]),
 dict(key='RetroPixelate', name='ピクセル化', transition=False, params=[
  n('SizeX','ピクセルサイズX',16,1,200,'px'),n('SizeY','ピクセルサイズY',16,1,200,'px'),e('Shape','ドット形状','四角|円形|六角'),e('Palette','減色','OFF|16色|64色|ゲームボーイ4階調',1),b('Grid','グリッド線',False),n('GridOpacity','グリッド不透明度',0.6,0,1),b('Dither','ディザリング'),c('グリッド線色',[12,12,15])], presets=[
  preset('レトロ16色'),preset('ゲームボーイ風',Palette=3,SizeX=8,SizeY=8),preset('ヘックス',Shape=2,Grid=True,Palette=0),preset('実用モザイク',Palette=0,Dither=False,SizeX=32,SizeY=32)]),
 dict(key='Heartbeat',name='鼓動',transition=False,params=[
  e('Mode','鼓動モード','ドッ・クン二連撃|サイン波'),n('Bpm','BPM',75,30,240),n('Scale','拡縮強度',8,0,30,'%'),n('Flash','周辺フラッシュ',0.5,0,1),n('Chromatic','色収差ブレ',5,0,15,'px'),c('フラッシュ色',[128,0,0])],presets=[
  preset('緊迫の心拍',Bpm=120),preset('恐怖・瀕死',Bpm=40,Scale=4,Flash=0.7),preset('ポップ弾み',Mode=1,Bpm=100,Scale=12,Flash=0,Chromatic=0)]),
 dict(key='RgbGaming',name='七色ゲーミング',transition=False,params=[
  e('Mode','発光モード','全体色相回転|流動光波|輪郭ネオン',1),n('Speed','回転速度',5,0,50),n('Angle','波の角度',45,0,360,'°'),n('Intensity','発光強度',2,1,5),e('Blend','合成モード','通常|加算|スクリーン|色相置換')],presets=[
  preset('激流ウェーブ'),preset('爆速カラー',Speed=40,Intensity=4,Blend=1),preset('ネオン輪郭',Mode=2,Speed=3),preset('ゆったりムーディー',Speed=0.4,Intensity=1,Blend=2)]),
 dict(key='BlockWave',name='ブロックウェーブ',transition=True,params=[
  n('Count','分割数',20,5,100),e('Direction','波の進行方向','水平|垂直|中央放射'),e('Waveform','波の形状','正弦波|デジタルステップ'),n('Amplitude','ズレ強度',30,0,150,'px'),n('Speed','波の速度',2,0.1,10),b('Border','ブロック境界線'),n('Progress','進行度（切替用）',100,0,100,'%')],presets=[
  preset('デジタル水平波',Waveform=1),preset('中央リップル',Direction=2,Count=35),preset('立体ブロック',Count=10,Amplitude=60)]),
 dict(key='LivingFlame',name='炎・画像炎化',transition=False,params=[
  n('Distortion','うねり変形度',30,0,100),n('Speed','揺らぎ速度',3,0.1,10),b('Colorize','炎着色'),e('Palette','炎パレット','赤黄オレンジ|青白い炎|紫炎'),b('Sparks','火の粉'),n('Dissolve','燃焼ディゾルブ',0,0,100,'%'),n('ColorMix','着色ブレンド',0.85,0,1)],presets=[
  preset('紅蓮の業火'),preset('陽炎・元色保持',Colorize=False,Sparks=False,Distortion=12),preset('青い鬼火',Palette=1),preset('紫の魔炎',Palette=2)]),
 dict(key='GodRays',name='ゴッドレイ・多条光線',transition=False,params=[
  n('LightX','光源X',0.5,-1,2),n('LightY','光源Y',-0.2,-1,2),n('Density','光筋の密度',24,5,50),n('Length','光条の長さ',0.85,0.1,1),n('Noise','雲間ノイズ',0.6,0,1),n('Width','光筋の太さ',0.4,0.05,1),c('光の色',[255,243,208])],presets=[
  preset('降り注ぐ木漏れ日'),preset('神の降臨',LightY=0.5,Density=40,Noise=0.25),preset('夕日サンセット',LightX=1.1,LightY=0.15,Tint=[255,134,48])]),
 dict(key='MatrixRain',name='マトリックス',transition=True,params=[
  e('Mode','合成モード','コード重ね|画像コード分解'),n('Speed','落下速度',2.5,0.5,10),n('Size','文字サイズ',18,8,48,'px'),e('Charset','文字種','英数・記号|カタカナ混じり|01バイナリ'),n('Tail','残光の長さ',14,5,30,'文字'),n('Progress','進行度（切替用）',100,0,100,'%'),c('文字カラー',[0,255,65])],presets=[
  preset('クラシックグリーン'),preset('元画像コード分解',Mode=1),preset('01バイナリ雨',Charset=2,Size=14)]),
 dict(key='Thunder',name='サンダー',transition=False,params=[
  e('Mode','動作モード','落雷フラッシュ|輪郭帯電オーラ'),n('Frequency','バチバチ頻度',12,1,30,'fps'),n('Thickness','稲妻の太さ',4,1,20,'px'),n('Jaggedness','ギザギザ・分岐度',6,1,10),n('Flash','閃光強度',0.7,0,1),c('電撃カラー',[176,226,255])],presets=[
  preset('超雷撃'),preset('覚醒帯電オーラ',Mode=1,Flash=0),preset('漆黒の魔雷',Mode=1,Tint=[174,50,255],Flash=0)]),
 dict(key='CameraShakeZoom',name='スマートズームブラー＆シェイク',transition=False,params=[
  n('Shake','揺れ強度',25,0,100,'px'),n('Blur','放射ブラー強度',0.4,0,1),n('Frequency','振動速度',24,1,60,'Hz'),n('Zoom','中心ズーム率',12,0,50,'%'),n('Decay','減衰時間',0.5,0.1,2,'秒')],presets=[
  preset('ビートドロップ'),preset('激震ツッコミ',Shake=70,Blur=0.65,Decay=0.3),preset('映画的スロー',Shake=8,Frequency=5,Decay=1.5)]),
 dict(key='FilmBurn',name='フィルムバーン＆ライトリーク',transition=True,params=[
  n('Progress','進行度',0,0,100,'%'),e('Palette','光の色調','ウォームオレンジ|パステルプリズム|クールシアン'),n('Width','感光の広がり幅',0.8,0.1,2),b('Grain','フィルム粒子'),b('Darken','焼きつき暗転',False)],presets=[
  preset('ヴィンテージオレンジ'),preset('シネマティックプリズム',Palette=1,Width=1.2),preset('フラッシュ切替',Width=1.8,Grain=False)]),
 dict(key='MotionGhost',name='モーションゴースト・RGB残像',transition=False,params=[
  n('Delay','残像遅延',6,1,30,'フレーム'),n('Opacity','ゴースト不透明度',0.5,0,1),n('Split','RGB分離強度',15,0,50,'px'),n('Echo','拡大エコー率',5,0,30,'%'),n('Fade','フェードアウト速度',2,1,5)],presets=[
  preset('RGBスプリットゴースト'),preset('半透明エコー',Split=0,Delay=12,Echo=10),preset('フラッシュ残像',Delay=3,Opacity=0.8,Fade=4)]),
 dict(key='VhsGlitch',name='レトロVHSグリッチ',transition=False,params=[
  n('Intensity','グリッチ強度',0.3,0,1),n('Scanlines','走査線濃度',0.4,0,1),b('Tracking','下部トラッキング帯'),n('Frequency','水平同期ズレ頻度',0.3,0,1),b('Fade','テープ色あせ')],presets=[
  preset('80sホームビデオ'),preset('激しいトラッキング',Intensity=0.9,Frequency=0.8),preset('深夜のホラーテープ',Intensity=0.6,Scanlines=0.8)]),
 dict(key='InkBleed',name='インクにじみ',transition=True,params=[
  n('Progress','進行度',0,0,100,'%'),n('Scale','にじみ粒度',6,1,20),n('Softness','にじみ幅',0.12,0.01,0.5),n('Seed','シード',7,0,1000),n('CenterX','中心X',0.5,0,1),n('CenterY','中心Y',0.5,0,1),c('インク縁色',[25,18,38])],presets=[
  preset('水彩にじみ'),preset('墨の飛沫',Scale=12,Softness=0.04,Tint=[3,3,3]),preset('パステルインク',Scale=3,Softness=0.3,Tint=[194,104,171])]),
 dict(key='LumaMelt',name='ルミナンス・メルト',transition=True,params=[
  n('Progress','進行度',0,0,100,'%'),n('Softness','境界の柔らかさ',0.1,0.01,0.5),n('Distortion','溶ける変形',20,0,100,'px'),b('BrightFirst','明るい場所から',True)],presets=[
  preset('ハイライト溶解'),preset('暗部から溶解',BrightFirst=False),preset('液状メルト',Distortion=80,Softness=0.25)]),
 dict(key='PrismWhip',name='プリズム・ウィップ',transition=True,params=[
  n('Progress','進行度',0,0,100,'%'),n('Angle','パン角度',0,0,360,'°'),n('Blur','方向ブラー',0.5,0,1),n('Chromatic','色収差',12,0,40,'px'),n('Zoom','ズーム',15,0,50,'%')],presets=[
  preset('水平ウィップ'),preset('縦ウィップ',Angle=90),preset('斜めプリズム',Angle=35,Chromatic=30,Blur=0.8)])
]

def literal(p, value, prefix):
    t=p['type']
    if t=='number': return f"new Animation({value}, {p['low']}, {p['high']})"
    if t=='bool': return str(value).lower()
    if t=='enum': return f"{prefix}{p['key']}Choice.Option{value}"
    return 'Color.FromRgb('+', '.join(map(str,value))+')'

def render(effect, idx):
    key=effect['key']; params=effect['params']; presets=effect['presets']
    defaults={p['key']:p['default'] for p in params}
    defaults.update(presets[0]['values'])
    lines=['// Generated by tools/generate_effects.py. Edit the catalog, then regenerate.',
      'using System.ComponentModel.DataAnnotations;', 'using System.Numerics;', 'using System.Windows.Media;',
      'using Newtonsoft.Json;', 'using YukkuriMovieMaker.Commons;', 'using YukkuriMovieMaker.Controls;',
      'using YukkuriMovieMaker.Plugin.Effects;', 'using YMM4.ExtendedEffectsPack.Common;', '',
      'namespace YMM4.ExtendedEffectsPack.Effects;', '',f'public enum {key}Preset', '{']
    for i,p in enumerate(presets): lines.append(f'    [Display(Name = "{p["name"]}")] Option{i},')
    lines+=['}', '']
    for p in params:
        if p['type']=='enum':
            lines += [f'public enum {key}{p["key"]}Choice', '{']
            for i,o in enumerate(p['options']): lines.append(f'    [Display(Name = "{o}")] Option{i},')
            lines += ['}', '']
    lines += [f'[VideoEffect("EEP / {effect["name"]}", ["Extended Effects Pack"], ["EEP"], IsAviUtlSupported = false)]',
        f'public sealed class {key}Effect : PackEffect', '{',f'    public override string Label => "{effect["name"]}";',
        f'    public override int EffectId => {idx};',f'    private {key}Preset preset;',
        '    [JsonProperty(Order = -100)]', '    [Display(Name = "プリセット", Order = 0, Description = "選択すると各設定とキーフレームを初期化します。")]',
        '    [EnumComboBox]',f'    public {key}Preset Preset', '    {', '        get => preset;',
        '        set { if (Set(ref preset, value)) ApplyPreset(value); }', '    }', '']
    for i,p in enumerate(params):
        typ={'number':'Animation','color':'Color','bool':'bool','enum':f'{key}{p["key"]}Choice'}[p['type']]
        initial=literal(p,defaults[p['key']],key)
        control={'color':'ColorPicker','bool':'ToggleSlider','enum':'EnumComboBox'}.get(p['type'])
        if not control: control=f'AnimationSlider("F2", "{p["unit"]}", {p["low"]}, {p["high"]})'
        field=p['key'][0].lower()+p['key'][1:]
        lines += [f'    private {typ} {field} = {initial};',f'    [Display(Name = "{p["label"]}", Order = {i+1})]', f'    [{control}]',
            f'    public {typ} {p["key"]} {{ get => {field}; set => Set(ref {field}, value); }}','']
    lines += [f'    private void ApplyPreset({key}Preset value)', '    {', '        switch (value)', '        {']
    for i,pre in enumerate(presets):
        values={p['key']:p['default'] for p in params}; values.update(pre['values'])
        lines += [f'            case {key}Preset.Option{i}:']
        for p in params: lines += [f'                {p["key"]} = {literal(p,values[p["key"]],key)};']
        lines += ['                break;']
    lines += ['        }','    }','']
    vals=[]; anim=[]
    for p in params:
        k=p['key']; t=p['type']
        if t=='number': vals.append(f'Value({k}, frame, length, fps)'); anim.append(k)
        elif t=='bool': vals.append(f'{k} ? 1 : 0')
        elif t=='enum': vals.append(f'(float){k}')
    lines += ['    internal override float[] ReadValues(double frame, double length, int fps) =>', '        ['+', '.join(vals)+'];',
      '    protected override IEnumerable<IAnimatable> GetAnimatables() => ['+', '.join(anim)+'];']
    if any(p['type']=='color' for p in params): lines += ['    internal override Vector4 ReadTint() => new(Tint.R / 255f, Tint.G / 255f, Tint.B / 255f, Tint.A / 255f);']
    if key=='Heartbeat': lines += ['    internal override double ReadClock(double frame, double length, int fps) => IntegratedBeats(Bpm, frame, length, fps);']
    lines += ['}','']
    return '\n'.join(lines)

def render_transition(effect, idx):
    import re
    key = effect['key']
    body = render(effect, idx).split('[VideoEffect(', 1)[1].split('\n', 1)[1]
    body = body.replace(f'public sealed class {key}Effect : PackEffect', f'public sealed class {key}TransitionParameter : PackTransitionParameter')
    body = re.sub(r'    public override string Label => .*?;\n', '', body)
    # These values are driven by the transition host, not by user progress/loop controls.
    for hidden in ['Progress', 'AutoLoop', 'Frequency'] if key == 'Blink' else ['Progress']:
        body = re.sub(r'    \[Display\([^\n]*\)\]\n    \[[^\n]+\]\n(    public [^\n]+ ' + hidden + r' \{)', r'    [System.ComponentModel.Browsable(false)]\n\1', body)
    return '\n'.join([
        '// Generated by tools/generate_effects.py.',
        'using System.ComponentModel.DataAnnotations;', 'using System.Numerics;',
        'using System.Windows.Media;', 'using Newtonsoft.Json;', 'using YukkuriMovieMaker.Commons;',
        'using YukkuriMovieMaker.Controls;', 'using YukkuriMovieMaker.Plugin.Transition;',
        'using YMM4.ExtendedEffectsPack.Effects;', '',
        'namespace YMM4.ExtendedEffectsPack.Transitions;', '',
        f'public sealed class {key}TransitionPlugin : ITransitionPlugin', '{',
        f'    public string Name => "EEP / {effect["name"]}";',
        f'    public ITransitionParameter CreateTransitionParameter() => new {key}TransitionParameter();', '}', '', body])

if __name__=='__main__':
    for idx, effect in enumerate(CATALOG):
        (OUT / (effect['key']+'Effect.cs')).write_text(render(effect,idx))
        if effect['transition']:
            (OUT.parent/'Transitions'/(effect['key']+'Transition.cs')).write_text(render_transition(effect,idx))
    (ROOT/'tools/effect_catalog.json').write_text(json.dumps(CATALOG,ensure_ascii=False,indent=2)+'\n')
