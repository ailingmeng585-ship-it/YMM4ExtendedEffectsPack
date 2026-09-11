"""Portable structural checks. These do not substitute for Windows GPU tests."""
import importlib.util
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('generator', ROOT/'tools/generate_effects.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)
CATALOG = generator.CATALOG
SRC = ROOT/'src/YMM4.ExtendedEffectsPack'

class CatalogTests(unittest.TestCase):
    def test_registry(self):
        self.assertEqual(len(CATALOG), 17)
        self.assertEqual(sum(e['transition'] for e in CATALOG), 8)
        self.assertEqual(len({e['key'] for e in CATALOG}), 17)

    def test_controls_and_presets(self):
        for e in CATALOG:
            self.assertLessEqual(1+len(e['params']), 10, e['key'])
            self.assertTrue(3 <= len(e['presets']) <= 5)
            visible = len(e['params'])-sum(p['key']=='Progress' for p in e['params'])
            if e['key']=='Blink': visible -= 2
            if e['transition']: self.assertLessEqual(visible+3, 10, e['key'])

    def test_preset_values_are_valid(self):
        for e in CATALOG:
            known={p['key']:p for p in e['params']}
            for pre in e['presets']:
                self.assertTrue(set(pre['values']) <= known.keys())
                for key, p in known.items():
                    value=pre['values'].get(key,p['default'])
                    if p['type']=='number': self.assertTrue(p['low'] <= value <= p['high'], (e['key'], key))
                    elif p['type']=='enum': self.assertTrue(0 <= value < len(p['options']))
                    elif p['type']=='color': self.assertTrue(len(value)==3 and all(0<=x<=255 for x in value))
                    elif p['type']=='bool': self.assertIsInstance(value,bool)

    def test_generated_sources_match_catalog(self):
        for i,e in enumerate(CATALOG):
            self.assertEqual((SRC/'Effects'/(e['key']+'Effect.cs')).read_text(),generator.render(e,i))
            if e['transition']:
                self.assertEqual((SRC/'Transitions'/(e['key']+'Transition.cs')).read_text(),generator.render_transition(e,i))
        self.assertEqual(json.loads((ROOT/'tools/effect_catalog.json').read_text()),CATALOG)

    def test_every_numeric_control_is_animatable(self):
        for e in CATALOG:
            source=(SRC/'Effects'/(e['key']+'Effect.cs')).read_text()
            anim=re.search(r'GetAnimatables\(\) => \[(.*?)\]',source)[1]
            for p in e['params']:
                if p['type']=='number':
                    self.assertIn('Animation '+p['key'],source)
                    self.assertIn(p['key'],anim)
            self.assertIn('[JsonProperty(Order = -100)]',source)

    def test_transition_progress_controls_are_hidden(self):
        for e in CATALOG:
            if not e['transition']: continue
            source=(SRC/'Transitions'/(e['key']+'Transition.cs')).read_text()
            self.assertRegex(source,r'Browsable\(false\)\]\s+public Animation Progress')
            self.assertIn(': ITransitionPlugin',source)

    def test_shader_ids_and_constant_layout(self):
        shader=(SRC/'Shaders/Pack.hlsl').read_text()
        ids=list(map(int,re.findall(r'case (\d+): result=',shader)))
        self.assertEqual(ids,list(range(17)))
        fields=['Rect','Meta','A','B','C','Tint','History']
        constants=shader.split('cbuffer Constants',1)[1].split('};',1)[0]
        self.assertEqual(re.findall(r'float4 (\w+);',constants),fields)
        cs=(SRC/'Common/PackShader.cs').read_text()
        self.assertEqual(re.findall(r'public Vector4 (\w+);',cs),fields)

    def test_nonlocal_rectangle_invalidation(self):
        cs=(SRC/'Common/PackShader.cs').read_text()
        self.assertIn('MapInvalidRect(int inputIndex, RawRect invalidInputRect) => Bounds',cs)
        self.assertIn('inputs[i] = Bounds',cs)

    def test_shader_endpoints_and_alpha_contract(self):
        shader=(SRC/'Shaders/Pack.hlsl').read_text()
        self.assertIn('Meta.z<=0) return sample0(uv)',shader)
        self.assertIn('Meta.z>=1) return sample1(uv)',shader)
        self.assertIn('result.rgb=clamp(result.rgb,0,result.a)',shader)
        self.assertIn('const float3 colors[16]',shader)

    def test_single_assembly_distribution(self):
        project=(SRC/'YMM4.ExtendedEffectsPack.csproj').read_text()
        self.assertIn('<Private>false</Private>',project)
        self.assertIn('<EmbeddedResource Include="Shaders/*.hlsl"',project)
        self.assertNotIn('PackageReference',project)
        self.assertIn('net10.0-windows10.0.19041.0',project)

if __name__=='__main__': unittest.main(verbosity=2)
