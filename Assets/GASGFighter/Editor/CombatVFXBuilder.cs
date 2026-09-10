using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace GASG.Fighting.Editor
{
    // Unity 6000.3.11f1 / URP・VFX Graph 17.3 専用のオーサリングツール。
    // Editorで標準ノードを組み立てるだけで、再生時の粒子制御コードは生成しない。
    public static class CombatVFXBuilder
    {
        public const string Root = "Assets/GASGFighter/VFX/CombatVFX";
        static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly Color Ice = new Color(.14f, .57f, 1f);
        static readonly Color White = new Color(.68f, .88f, 1f);
        static readonly Color Amber = new Color(1f, .32f, .035f);
        static readonly Color Teal = new Color(.1f, 1f, .77f);

        [MenuItem("GASG/VFX/Create Missing Combat VFX Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("再生を停止してから実行してください。");
            if (!(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))
                throw new InvalidOperationException("URPが設定されていません。");
            try
            {
                Folder(Root + "/Graphs"); Folder(Root + "/Prefabs");
                CombatVFXArtAssets.Generate(Root);
                var names = new[] { "Hadouken", "Shoryuken", "Hit", "Guard", "NormalAttack" };
                for (int i = 0; i < names.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Combat VFX", names[i] + " の標準Graphを生成", (float)i / names.Length);
                    BuildEffect(names[i]);
                }
                CreateLibrary();
                AssetDatabase.SaveAssets();
                Debug.Log("[Combat VFX][成功] 5種類のGraph/Prefabを確認しました: " + Root);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("GASG/VFX/Dry Run - Check Output Paths")]
        public static void DryRun()
        {
            foreach (string n in new[] { "Hadouken", "Shoryuken", "Hit", "Guard", "NormalAttack" })
                Debug.Log("[Combat VFX][Dry Run] " + n + ": " + (File.Exists(Root + "/Graphs/VFX_" + n + ".vfx") ? "既存を保持" : "新規作成予定"));
        }

        static void BuildEffect(string name)
        {
            string path = Root + "/Graphs/VFX_" + name + ".vfx";
            VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (!File.Exists(path))
            {
                asset = (VisualEffectAsset)Call(T("UnityEditor.VisualEffectAssetEditorUtility"), "CreateNewAsset", path);
                if (!asset) throw new IOException("Graph作成失敗: " + path);
                var g = new Graph(asset);
                if (name == "Hadouken") Hadouken(g);
                else if (name == "Shoryuken") Shoryuken(g);
                else if (name == "Hit") Impact(g, "OnPlay", false);
                else if (name == "Guard") Impact(g, "OnPlay", true);
                else Normal(g);
                g.Save();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                Debug.Log("[Combat VFX][成功] Graph: " + path);
            }
            else Debug.Log("[Combat VFX][スキップ] 既存Graphを保持: " + path);
            string prefabPath = Root + "/Prefabs/PF_" + name + "_VFX.prefab";
            if (File.Exists(prefabPath)) { Debug.Log("[Combat VFX][スキップ] " + prefabPath); return; }
            if (!asset) throw new IOException("Graphが読み込めません: " + path);
            var go = new GameObject("PF_" + name + "_VFX");
            try
            {
                var vfx = go.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = asset;
                vfx.resetSeedOnPlay = true;
                vfx.initialEventName = "OnPlay";
                if (!PrefabUtility.SaveAsPrefabAsset(go, prefabPath)) throw new IOException(prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void CreateLibrary()
        {
            const string path = "Assets/GASGFighter/Resources/FighterVfxLibrary.asset";
            if (File.Exists(path)) { Debug.Log("[Combat VFX][スキップ] 既存の再生設定を保持"); return; }
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GASG.Fighting.FighterVfxLibrary")).FirstOrDefault(t => t != null);
            if (type == null) { Debug.LogWarning("[Combat VFX][スキップ] 再生設定クラスがありません。Prefab単体は利用できます。"); return; }
            Folder("Assets/GASGFighter/Resources");
            var library = ScriptableObject.CreateInstance(type);
            var so = new SerializedObject(library);
            foreach (var n in new[] { "Hadouken", "Shoryuken", "Hit", "Guard", "NormalAttack" })
            {
                string field = char.ToLowerInvariant(n[0]) + n.Substring(1) + "Prefab";
                var p = so.FindProperty(field);
                if (p == null) throw new MissingFieldException(type.Name, field);
                p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/PF_" + n + "_VFX.prefab");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(library, path);
        }

        // 各レイヤーの寿命・発生数・色・幅・軌跡・速度・ノイズをBlackboardに公開。
        sealed class Layer
        {
            public string name, mesh, texture = "T_Soft", evt = "OnPlay";
            public bool loop, smoke, radial, align;
            public float count = 1, life = .25f, delay, size = 1, brightness = 2, noise, drag;
            public Vector3 pos, spread, velocity, jitter, scale = Vector3.one, angle, spin;
            public Color color = Ice;
            public AnimationCurve fade = Curve(0, 0, .06f, 1, .35f, .7f, 1, 0);
            public AnimationCurve grow = Curve(0, .3f, .2f, .85f, 1, 1.25f);
        }

        static void Hadouken(Graph g)
        {
            g.Layer(new Layer { name="Flight_Core", loop=true, count=70, life=.085f, size=.68f, brightness=4.3f, color=White, pos=new Vector3(.15f,0,0), spread=new Vector3(.07f,.05f,.04f), scale=new Vector3(1.12f,.82f,1), grow=Curve(0,.7f,.4f,1,1,.75f) });
            g.Layer(new Layer { name="Flight_BlueHalo", loop=true, count=28, life=.12f, size=1.2f, brightness=.85f, color=Ice, pos=new Vector3(.03f,0,0), scale=new Vector3(1.35f,1,1) });
            g.Layer(new Layer { name="Flight_Vortex", mesh="M_OrbRibbon", texture="T_Filament", loop=true, count=19, life=.19f, brightness=3.6f, color=Ice, angle=new Vector3(22,0,0), spin=new Vector3(440,0,0), scale=new Vector3(1,1.05f,1.05f), grow=Curve(0,.82f,.25f,1,1,1.08f) });
            g.Layer(new Layer { name="Flight_WhiteFilaments", mesh="M_OrbRibbon", texture="T_Filament", loop=true, count=11, life=.16f, brightness=4.2f, color=White, angle=new Vector3(133,0,0), spin=new Vector3(-620,0,0), scale=new Vector3(1.3f,.72f,.72f), size=.92f });
            g.Layer(new Layer { name="Flight_SpeedLines", texture="T_Spark", loop=true, count=110, life=.18f, size=.55f, brightness=2.7f, color=Ice, spread=new Vector3(.15f,.31f,.24f), velocity=new Vector3(-7,0,0), jitter=new Vector3(2,.45f,.4f), scale=new Vector3(.075f,1.8f,1), align=true, noise=2, grow=Curve(0,.3f,.15f,1,1,.25f) });
            g.Layer(new Layer { name="Flight_Embers", texture="T_Spark", loop=true, count=70, life=.32f, size=.055f, brightness=3, color=White, spread=new Vector3(.35f,.37f,.3f), velocity=new Vector3(-3.5f,0,0), jitter=new Vector3(1,.8f,.8f), noise=3, align=true, scale=new Vector3(.5f,2,1) });
            g.Layer(new Layer { name="Flight_Vapor", texture="T_Smoke", loop=true, smoke=true, count=18, life=.42f, size=.52f, brightness=.65f, color=new Color(.15f,.27f,.45f), pos=new Vector3(-.55f,0,0), spread=new Vector3(.25f,.25f,.2f), velocity=new Vector3(-2.8f,.1f,0), noise=1.2f, spin=new Vector3(0,0,65), scale=new Vector3(1.5f,1,1), grow=Curve(0,.45f,.4f,1,1,1.5f) });
            g.Layer(new Layer { name="Release_Ring", mesh="M_Ring", texture="T_Filament", count=1, life=.2f, size=.78f, brightness=3.5f, color=White, angle=new Vector3(0,62,0), scale=new Vector3(.75f,1,1), grow=Curve(0,.2f,.3f,.8f,1,1.5f) });
            Impact(g,"OnImpact",false,"Impact_");
            Impact(g,"OnGuard",true,"Guard_");
            g.Layer(new Layer { name="Charge_Wind", evt="OnCharge", mesh="M_OrbRibbon", texture="T_Filament", count=3, life=.22f, size=.6f, brightness=2.4f, spin=new Vector3(550,0,0), grow=Curve(0,1.2f,1,.2f) });
        }

        static void Shoryuken(Graph g)
        {
            g.Layer(new Layer { name="Ground_Flash", texture="T_Flash", size=1.1f, life=.12f, brightness=4, color=White, pos=new Vector3(0,.12f,0), scale=new Vector3(1.6f,.55f,1) });
            g.Layer(new Layer { name="Ground_Shockwave", mesh="M_Ring", texture="T_Filament", size=.95f, life=.33f, brightness=2.6f, angle=new Vector3(82,0,0), pos=new Vector3(0,.07f,0), grow=Curve(0,.15f,.2f,.8f,1,1.8f) });
            g.Layer(new Layer { name="Rising_Core", mesh="M_Helix", texture="T_Filament", count=2, life=.44f, delay=.035f, brightness=4.1f, color=White, spin=new Vector3(0,290,0), scale=new Vector3(.72f,1,.72f), grow=Curve(0,.25f,.18f,.72f,.55f,1,1,1.06f) });
            g.Layer(new Layer { name="Rising_BlueSpiral", mesh="M_Helix", texture="T_Filament", count=2, life=.58f, delay=.055f, brightness=2.8f, color=Ice, angle=new Vector3(0,125,-8), spin=new Vector3(0,-260,0), scale=new Vector3(1.2f,1,1.2f), grow=Curve(0,.22f,.28f,.85f,.6f,1,1,1.1f) });
            g.Layer(new Layer { name="Fist_Flare", texture="T_Flash", count=1, delay=.065f, life=.32f, brightness=3.7f, color=White, size=.62f, pos=new Vector3(.18f,.75f,0), velocity=new Vector3(.35f,5.4f,0), scale=new Vector3(.6f,1.3f,1) });
            g.Layer(new Layer { name="Rising_Streaks", texture="T_Spark", count=76, life=.38f, delay=.035f, brightness=3.1f, size=.42f, pos=new Vector3(0,.1f,0), spread=new Vector3(.48f,.12f,.32f), velocity=new Vector3(.15f,6,0), jitter=new Vector3(1.4f,2,1), scale=new Vector3(.065f,1.6f,1), align=true, noise=2, drag=.9f });
            g.Layer(new Layer { name="Ground_Dust", texture="T_Smoke", smoke=true, count=20, life=.72f, brightness=.8f, color=new Color(.37f,.42f,.51f), size=.56f, pos=new Vector3(0,.15f,0), spread=new Vector3(.4f,.09f,.3f), velocity=new Vector3(0,.6f,0), jitter=new Vector3(1.9f,.4f,1), noise=1.1f, drag=1.5f, spin=new Vector3(0,0,50), grow=Curve(0,.2f,.35f,1,1,1.5f) });
            g.Layer(new Layer { name="Fading_Motes", count=44, size=.045f, delay=.12f, life=.65f, color=Ice, brightness=3, pos=new Vector3(0,.8f,0), spread=new Vector3(.5f,.55f,.4f), velocity=new Vector3(.1f,2.1f,0), jitter=new Vector3(1,.7f,.8f), noise=2 });
        }

        static void Impact(Graph g,string evt,bool guard,string prefix="")
        {
            Color tint=guard?Teal:Amber;
            g.Layer(new Layer { name=prefix+"Contact_Flash", evt=evt, texture="T_Flash", life=.085f, size=guard?.72f:1.1f, brightness=5.2f, color=White, scale=new Vector3(1.3f,.85f,1), grow=Curve(0,.55f,.16f,1,1,.2f) });
            g.Layer(new Layer { name=prefix+"Contact_Halo", evt=evt, life=.16f, size=guard?.8f:1.25f, brightness=1.1f, color=tint });
            g.Layer(new Layer { name=prefix+(guard?"Shield_Crescent":"Shock_Ring"), evt=evt, mesh=guard?"M_Crescent":"M_Ring", texture="T_Filament", life=guard?.26f:.3f, delay=.025f, size=guard?.92f:.63f, brightness=guard?3.5f:2.5f, color=guard?Teal:Ice, angle=new Vector3(0,guard?10:46,guard?0:18), pos=new Vector3(guard?-.15f:0,0,0), grow=Curve(0,.2f,.2f,.85f,1,1.45f) });
            if(guard) g.Layer(new Layer { name=prefix+"Shield_InnerEdge", evt=evt, mesh="M_Crescent", texture="T_Filament", life=.18f, size=.76f, brightness=4, color=White, pos=new Vector3(-.14f,0,-.025f), grow=Curve(0,.35f,.3f,.95f,1,1.1f) });
            g.Layer(new Layer { name=prefix+"Main_Sparks", evt=evt, texture="T_Spark", count=guard?34:62, life=guard?.24f:.34f, size=guard?.32f:.52f, delay=.015f, brightness=4, color=tint, spread=new Vector3(.08f,.08f,.03f), velocity=new Vector3(guard?-1:.6f,.2f,0), jitter=new Vector3(6,5,.8f), scale=new Vector3(.075f,1.8f,1), align=true, drag=2.6f, grow=Curve(0,.1f,.15f,1,1,.08f) });
            g.Layer(new Layer { name=prefix+"Blue_Shards", evt=evt, texture="T_Spark", count=guard?22:24, life=.4f, size=.14f, delay=.035f, brightness=3.2f, color=Ice, spread=new Vector3(.08f,.08f,.04f), jitter=new Vector3(4,4.8f,1.4f), scale=new Vector3(.25f,1,1), align=true, noise=1.5f, drag=2 });
            g.Layer(new Layer { name=prefix+"Residual_Motes", evt=evt, count=guard?18:38, life=.64f, size=.038f, delay=.055f, brightness=3.1f, color=tint, jitter=new Vector3(3,3.2f,1), drag=3, noise=1 });
            g.Layer(new Layer { name=prefix+"Impact_Smoke", evt=evt, texture="T_Smoke", smoke=true, count=guard?6:11, life=.68f, size=guard?.48f:.64f, delay=.045f, brightness=.85f, color=new Color(.37f,.43f,.53f), jitter=new Vector3(1.25f,1.4f,.3f), velocity=new Vector3(0,.25f,0), noise=.7f, drag=2, spin=new Vector3(0,0,80), grow=Curve(0,.25f,.25f,.85f,1,1.5f) });
        }

        static void Normal(Graph g)
        {
            g.Layer(new Layer { name="Punch_WhiteWind", mesh="M_PunchArc", texture="T_Filament", count=1, life=.19f, brightness=1.9f, color=new Color(.55f,.7f,.87f), size=.85f, velocity=new Vector3(.8f,0,0), grow=Curve(0,.6f,.3f,1,1,1.2f) });
            g.Layer(new Layer { name="Punch_FineEdge", mesh="M_PunchArc", texture="T_Filament", count=1, life=.12f, brightness=2.2f, color=White, size=.68f, angle=new Vector3(0,10,8), pos=new Vector3(.12f,0,-.025f), grow=Curve(0,.75f,.3f,1,1,1.1f) });
            g.Layer(new Layer { name="Punch_SpeedLines", texture="T_Spark", count=12, life=.14f, brightness=1.7f, color=White, size=.25f, spread=new Vector3(.18f,.15f,.08f), velocity=new Vector3(4.3f,0,0), jitter=new Vector3(1,.4f,.2f), scale=new Vector3(.06f,1.3f,1), align=true });
            g.Layer(new Layer { name="Punch_Air", texture="T_Smoke", smoke=true, count=4, life=.29f, size=.25f, brightness=.6f, color=new Color(.45f,.5f,.58f), pos=new Vector3(-.18f,0,0), spread=new Vector3(.08f,.12f,.04f), velocity=new Vector3(.6f,.03f,0), noise=.5f, grow=Curve(0,.25f,.3f,.8f,1,1.4f) });
        }

        sealed class Graph
        {
            readonly object graph, resource;
            readonly Dictionary<string,object> events = new Dictionary<string,object>();
            readonly object brightness, size;
            int index, paramIndex;
            public Graph(VisualEffectAsset asset)
            {
                resource=Call(T("UnityEditor.VFX.VisualEffectObjectExtensions"),"GetResource",asset);
                graph=Call(T("UnityEditor.VFX.VisualEffectResourceExtensions"),"GetOrCreateGraph",resource);
                brightness=Param("Global_Brightness",1f,"Global");
                size=Param("Global_Size",1f,"Global");
            }
            object Node(string type,float x,float y)
            {
                object n=ScriptableObject.CreateInstance(T(type.Contains(".")?type:"UnityEditor.VFX."+type));
                Call(graph,"AddChild",n);
                Set(n,"position",new Vector2(x,y));
                return n;
            }
            object Param(string name,object value,string category)
            {
                var p=ScriptableObject.CreateInstance(T("UnityEditor.VFX.VFXParameter"));
                Call(p,"Init",value.GetType());
                Setting(p,"m_ExposedName",name); Setting(p,"m_Exposed",true);
                Set(p,"value",value); Set(p,"category",category); Set(p,"order",paramIndex++);
                Call(graph,"AddChild",p); Set(p,"position",new Vector2(-550,paramIndex*70));
                return Slot(p,false,0);
            }
            object Event(string name)
            {
                if(events.TryGetValue(name,out var n))return n;
                n=Node("VFXBasicEvent",-250,events.Count*180-250); Setting(n,"eventName",name);
                events.Add(name,n);return n;
            }
            object Block(object context,string name)
            {
                var b=ScriptableObject.CreateInstance(T(name.Contains(".")?name:"UnityEditor.VFX.Block."+name));
                Call(context,"AddChild",b);return b;
            }
            object Attr(object context,string name,object value,string composition="Overwrite")
            {
                var b=Block(context,"SetAttribute"); Setting(b,"attribute",name); Setting(b,"Composition",composition);
                Value(Slot(b,true,0),value);return b;
            }
            void RandomAttr(object context,string name,Vector3 a,Vector3 b)
            {
                var n=Block(context,"SetAttribute");Setting(n,"attribute",name);Setting(n,"Random","PerComponent");
                Value(Slot(n,true,0),a);Value(Slot(n,true,1),b);
            }
            void Expose(object target,int slot,string name,object val,string category) => Link(Param(name,val,category),Slot(target,true,slot));
            void CurveBlock(object output,string attr,AnimationCurve curve,string name,string category,string composition)
            {
                var b=Block(output,"AttributeFromCurve");Setting(b,"attribute",attr);Setting(b,"Composition",composition);Setting(b,"Mode","Uniform");
                Expose(b,0,name,curve,category);
            }
            public void Layer(Layer l)
            {
                float x=index++*410;
                object spawn=Node("VFXBasicSpawner",x,0),init=Node("VFXBasicInitialize",x,250),update=Node("VFXBasicUpdate",x,990),output=Node(l.mesh==null?"VFXPlanarPrimitiveOutput":"VFXMeshOutput",x,1420);
                Set(spawn,"label",l.name);Set(init,"label",l.name);Set(update,"label",l.name);Set(output,"label",l.name);
                Call(spawn,"LinkTo",init);Call(init,"LinkTo",update);Call(update,"LinkTo",output);
                Call(Event(l.evt),"LinkTo",spawn);
                if(l.loop)Call(Event("OnStop"),"LinkTo",spawn,0,1);
                Setting(init,"capacity",(uint)(l.loop?256:Mathf.NextPowerOfTwo(Mathf.Max(32,(int)l.count*2))));
                var data=Call(init,"GetData"); Set(data,"space",Enum.Parse(T("UnityEngine.VFX.VFXSpace"),"Local"));
                object bounds=Input(init,"bounds");
                if(bounds!=null)
                {
                    var box=Get(bounds,"value");Set(box,"center",new Vector3(0,1,0));Set(box,"size",new Vector3(18,14,10));Value(bounds,box);
                }
                object emitter=Block(spawn,l.loop?"UnityEditor.VFX.VFXSpawnerConstantRate":"UnityEditor.VFX.VFXSpawnerBurst");
                Expose(emitter,0,l.name+(l.loop?"_Rate":"_Count"),l.count,l.name);
                if(!l.loop)Expose(emitter,1,l.name+"_Delay",l.delay,l.name);
                object a=Attr(init,"lifetime",l.life);Expose(a,0,l.name+"_Lifetime",l.life,l.name);
                a=Attr(init,"size",l.size);Expose(a,0,l.name+"_Size",l.size,l.name);
                a=Attr(init,"size",1f,"Multiply");Link(size,Slot(a,true,0));
                a=Attr(init,"scale",l.scale);Expose(a,0,l.name+"_WidthLength",l.scale,l.name);
                a=Attr(init,"color",new Vector3(l.color.r,l.color.g,l.color.b));
                Expose(a,0,l.name+"_Color",new Vector3(l.color.r,l.color.g,l.color.b),l.name);
                a=Attr(init,"color",Vector3.one*l.brightness,"Multiply");
                Link(Param(l.name+"_Brightness",l.brightness,l.name),Slot(a,true,0));
                a=Attr(init,"color",Vector3.one,"Multiply");Link(brightness,Slot(a,true,0));
                RandomAttr(init,"position",l.pos-l.spread,l.pos+l.spread);
                a=Attr(init,"position",Vector3.zero,"Add");Expose(a,0,l.name+"_Offset",Vector3.zero,l.name);
                RandomAttr(init,"velocity",l.velocity-l.jitter,l.velocity+l.jitter);
                a=Attr(init,"velocity",Vector3.one,"Multiply");Expose(a,0,l.name+"_SpeedScale",1f,l.name);
                Attr(init,"angle",l.angle);
                a=Attr(init,"angularVelocity",l.spin);Expose(a,0,l.name+"_RotationSpeed",l.spin,l.name);
                if(l.mesh!=null && l.loop)RandomAttr(init,"angle",l.angle-new Vector3(170,0,0),l.angle+new Vector3(170,0,0));
                if(l.smoke)RandomAttr(init,"angle",new Vector3(0,0,-180),new Vector3(0,0,180));
                if(l.noise>0)
                {
                    var b=Block(update,"Turbulence");Setting(b,"Mode","Absolute");Expose(b,1,l.name+"_Noise",l.noise,l.name);
                }
                if(l.drag>0){var b=Block(update,"Drag");Value(Slot(b,true,0),l.drag);}
                Setting(output,"blendMode",l.smoke?"Alpha":"Additive");Setting(output,"cullMode","Off");
                Value(Input(output,"mainTexture"),AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+l.texture+".png"));
                if(l.mesh!=null)Value(Input(output,"mesh"),AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+l.mesh+".asset"));
                else {var b=Block(output,"Orient");Setting(b,"mode",l.align?"AlongVelocity":"FaceCameraPlane");}
                CurveBlock(output,"alpha",l.fade,l.name+"_Fade",l.name,"Overwrite");
                CurveBlock(output,"size",l.grow,l.name+"_SizeOverLife",l.name,"Multiply");
            }
            public void Save()
            {
                Call(T("UnityEditor.VFX.VisualEffectResourceExtensions"),"WriteAssetWithSubAssets",resource);
            }
        }

        static AnimationCurve Curve(params float[] keys)
        {
            var k=new Keyframe[keys.Length/2];for(int i=0;i<k.Length;i++)k[i]=new Keyframe(keys[i*2],keys[i*2+1]);
            return new AnimationCurve(k);
        }
        public static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        public static Type T(string name)
        {
            var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(a=>a!=null);
            if(t==null)throw new TypeLoadException("VFX Graph 17.3の型が見つかりません: "+name);return t;
        }
        public static object Call(object obj,string name,params object[] args)
        {
            Type t=obj as Type??obj.GetType();
            foreach(var m in t.GetMethods(Flags).Where(m=>m.Name==name&&!m.IsGenericMethodDefinition).OrderBy(m=>m.GetParameters().Length))
            {
                var p=m.GetParameters();if(p.Length<args.Length||p.Skip(args.Length).Any(p2=>!p2.IsOptional))continue;
                if(args.Where((a,i)=>a!=null&&!p[i].ParameterType.IsInstanceOfType(a)).Any())continue;
                var full=p.Select((a,i)=>i<args.Length?args[i]:a.DefaultValue).ToArray();
                try{return m.Invoke(obj is Type?null:obj,full);}catch(TargetInvocationException e){throw new InvalidOperationException(t.Name+"."+name+" : "+e.InnerException,e.InnerException);}
            }
            throw new MissingMethodException(t.FullName,name+"("+string.Join(",",args.Select(a=>a?.GetType().Name))+ ")");
        }
        public static object Get(object obj,string name)
        {
            for(Type t=obj.GetType();t!=null;t=t.BaseType)
            {
                var p=t.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(p!=null)return p.GetValue(obj);
                var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name,name);
        }
        public static void Set(object obj,string name,object value)
        {
            for(Type t=obj.GetType();t!=null;t=t.BaseType)
            {
                var p=t.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(p!=null&&p.CanWrite){p.SetValue(obj,value);return;}
                var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(obj,value);return;}
            }
            throw new MissingMemberException(obj.GetType().Name,name);
        }
        public static void Setting(object obj,string name,object value)
        {
            var setting=Call(obj,"GetSetting",name);var old=Get(setting,"value");
            if(old!=null&&old.GetType().IsEnum)value=Enum.Parse(old.GetType(),value.ToString());
            Call(obj,"SetSettingValue",name,value);
        }
        static object Slot(object n,bool input,int i)=>Call(n,input?"GetInputSlot":"GetOutputSlot",i);
        static object Input(object n,string name)=>((IEnumerable)Get(n,"inputSlots")).Cast<object>().FirstOrDefault(s=>(string)Get(s,"name")==name);
        static void Link(object source,object dest)
        {
            if(!(bool)Call(dest,"Link",source))throw new InvalidOperationException("Graphスロット接続に失敗しました。");
        }
        static void Value(object slot,object value)
        {
            if(slot==null)throw new InvalidOperationException("Graph入力スロットが存在しません。");
            var old=Get(slot,"value");
            if(old!=null&&value!=null&&!old.GetType().IsInstanceOfType(value))
            {
                var convert=old.GetType().GetMethods(BindingFlags.Static|BindingFlags.Public).FirstOrDefault(m=>m.Name=="op_Implicit"&&m.ReturnType==old.GetType()&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==value.GetType());
                if(convert!=null)value=convert.Invoke(null,new[]{value});
            }
            Set(slot,"value",value);
        }
    }
}
