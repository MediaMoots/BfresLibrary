using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace BfresLibrary.TextConvert
{
    public class ShapeAnimConvert
    {
        internal class ShapeAnimStuct
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public int FrameCount { get; set; }
            public bool Loop { get; set; }
            public bool Baked { get; set; }

            public List<VertexShapeAnimStruct> VertexShapeAnims { get; set; }

            [JsonProperty(ItemConverterType = typeof(NoFormattingConverter))]
            public Dictionary<string, object> UserData { get; set; } = new Dictionary<string, object>();
        }

        internal class VertexShapeAnimStruct
        {
            public string Name { get; set; }

            public List<float> BaseDataList = new List<float>();
            public List<KeyShapeAnimInfo> KeyShapeAnimInfos { get; set; }
            public List<CurveAnimHelper> Curves { get; set; }
        }

        internal class ParamConstant
        {
            public uint AnimTarget;
            public float Value;
        }

        public static string ToJson(ShapeAnim anim)
        {
            ShapeAnimStuct animConv = new ShapeAnimStuct();
            animConv.Name = anim.Name;
            animConv.Path = anim.Path;
            animConv.VertexShapeAnims = new List<VertexShapeAnimStruct>();
            animConv.FrameCount = anim.FrameCount;
            animConv.Loop = anim.Flags.HasFlag(ShapeAnim.ShapeAnimFlags.Looping);
            animConv.Baked = anim.Flags.HasFlag(ShapeAnim.ShapeAnimFlags.BakedCurve);

            foreach (var matAnim in anim.VertexShapeAnims)
            {
                VertexShapeAnimStruct vertexShapeAnimConv = new VertexShapeAnimStruct();
                animConv.VertexShapeAnims.Add(vertexShapeAnimConv);

                vertexShapeAnimConv.Curves = new List<CurveAnimHelper>();
                vertexShapeAnimConv.Name = matAnim.Name;
                if (matAnim.BaseDataList != null)
                    vertexShapeAnimConv.BaseDataList = matAnim.BaseDataList.ToList();
                if (matAnim.KeyShapeAnimInfos != null)
                    vertexShapeAnimConv.KeyShapeAnimInfos = matAnim.KeyShapeAnimInfos.ToList();

                foreach (var curve in matAnim.Curves)
                {
                    string target = curve.AnimDataOffset.ToString();

                    var convCurve = CurveAnimHelper.FromCurve(curve, target, false);
                    vertexShapeAnimConv.Curves.Add(convCurve);
                }
            }

            foreach (var param in anim.UserData.Values)
                animConv.UserData.Add($"{param.Type}|{param.Name}", param.GetData());

            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings();
                return settings;
            };

            return JsonConvert.SerializeObject(animConv, Formatting.Indented);
        }

        public static ShapeAnim FromJson(string json)
        {
            ShapeAnim anim = new ShapeAnim();
            FromJson(anim, json);
            return anim;
        }

        public static void FromJson(ShapeAnim anim, string json)
        {
            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings();
                return settings;
            };

            var animJson = JsonConvert.DeserializeObject<ShapeAnimStuct>(json);

            anim.Name = animJson.Name;
            anim.VertexShapeAnims = new List<VertexShapeAnim>();
            anim.UserData = UserDataConvert.Convert(animJson.UserData);
            anim.FrameCount = animJson.FrameCount;
            if (animJson.Loop)
                anim.Flags |= ShapeAnim.ShapeAnimFlags.Looping;
            if (animJson.Baked)
                anim.Flags |= ShapeAnim.ShapeAnimFlags.BakedCurve;

            anim.BindIndices = new ushort[animJson.VertexShapeAnims.Count];
            for (int i = 0; i < anim.BindIndices.Length; i++)
                anim.BindIndices[i] = 65535;

            anim.VertexShapeAnims.Clear();
            foreach (var matAnimJson in animJson.VertexShapeAnims)
            {
                VertexShapeAnim vertexShapeAnim = new VertexShapeAnim();
                anim.VertexShapeAnims.Add(vertexShapeAnim);

                vertexShapeAnim.Name = matAnimJson.Name;
                vertexShapeAnim.BaseDataList = matAnimJson.BaseDataList.ToArray();

                if (matAnimJson.KeyShapeAnimInfos != null)
                    vertexShapeAnim.KeyShapeAnimInfos = matAnimJson.KeyShapeAnimInfos.ToArray();

                if (matAnimJson.Curves != null)
                {
                    foreach (var curveJson in matAnimJson.Curves)
                    {
                        var target = uint.Parse(curveJson.Target);

                        var curve = CurveAnimHelper.GenerateCurve(curveJson, (uint)target, false);
                        vertexShapeAnim.Curves.Add(curve);
                    }
                }
            }
        }
    }
}
