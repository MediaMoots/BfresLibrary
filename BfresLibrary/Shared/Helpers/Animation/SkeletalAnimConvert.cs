using BfresLibrary.TextConvert;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Syroot.Maths;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BfresLibrary.Helpers
{
    public class NormalizeZeroConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Handle both double and float types and normalize -0 to 0.
            if (value is double dValue)
            {
                writer.WriteValue(dValue == 0.0 ? 0.0 : dValue);
            }
            else if (value is float fValue)
            {
                writer.WriteValue(fValue == 0.0f ? 0.0f : fValue);
            }
            else
            {
                throw new JsonSerializationException("Expected double or float value.");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(double) || objectType == typeof(float);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Only writing is supported.");
        }

        public override bool CanRead => false;
    }

    public class SkeletalAnimHelper
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int FrameCount { get; set; }
        public bool Loop { get; set; }
        public bool Baked { get; set; }
        public bool UseDegrees { get; set; } = true;

        [JsonConverter(typeof(StringEnumConverter))]
        public SkeletalAnimFlagsScale FlagsScale { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SkeletalAnimFlagsRotate FlagsRotate { get; set; }

        public List<BoneAnimHelper> BoneAnims { get; set; }

        [JsonProperty(ItemConverterType = typeof(NoFormattingConverter))]
        public Dictionary<string, object> UserData { get; set; } = new Dictionary<string, object>();

        public static string ToJson(SkeletalAnim anim)
        {
            SkeletalAnimHelper animConv = new SkeletalAnimHelper();
            animConv.Name = anim.Name;
            animConv.Path = anim.Path;
            animConv.Loop = anim.Loop;
            animConv.Baked = anim.Baked;
            animConv.FrameCount = anim.FrameCount;
            animConv.FlagsScale = anim.FlagsScale;
            animConv.FlagsRotate = anim.FlagsRotate;
            animConv.BoneAnims = new List<BoneAnimHelper>();

            foreach (var boneAnim in anim.BoneAnims)
            {
                BoneAnimHelper boneAnimConv = new BoneAnimHelper();
                boneAnimConv.Curves = new List<CurveAnimHelper>();
                boneAnimConv.Name = boneAnim.Name;
                Vector4F rotation = boneAnim.BaseData.Rotate;
                if (animConv.UseDegrees && animConv.FlagsRotate == SkeletalAnimFlagsRotate.EulerXYZ)
                {
                    rotation = new Vector4F(
                        rotation.X * CurveAnimHelper.Rad2Deg,
                        rotation.Y * CurveAnimHelper.Rad2Deg,
                        rotation.Z * CurveAnimHelper.Rad2Deg,
                        rotation.W);
                }

                boneAnimConv.BaseData = new BaseDataHelper()
                {
                    Flags = boneAnim.BaseData.Flags,
                    Rotate = rotation,
                    Translate = boneAnim.BaseData.Translate,
                    Scale = boneAnim.BaseData.Scale,
                };
                boneAnimConv.SegmentScaleCompensate = boneAnim.ApplySegmentScaleCompensate;
                boneAnimConv.UseBaseTranslation = boneAnim.FlagsBase.HasFlag(BoneAnimFlagsBase.Translate);
                boneAnimConv.UseBaseRotation = boneAnim.FlagsBase.HasFlag(BoneAnimFlagsBase.Rotate);
                boneAnimConv.UseBaseScale = boneAnim.FlagsBase.HasFlag(BoneAnimFlagsBase.Scale);
                animConv.BoneAnims.Add(boneAnimConv);

                foreach (var curve in boneAnim.Curves)
                {
                    string target = ((AnimTarget)curve.AnimDataOffset).ToString();
                    var convCurve = CurveAnimHelper.FromCurve(curve, target,
                        target.Contains("Rotate") && animConv.UseDegrees);
                    boneAnimConv.Curves.Add(convCurve);
                }
            }

            foreach (var param in anim.UserData.Values)
                animConv.UserData.Add($"{param.Type}|{param.Name}", param.GetData());

            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings();
                return settings;
            };

            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new NormalizeZeroConverter() }
            };

            return JsonConvert.SerializeObject(animConv, Formatting.Indented, settings);
        }

        public static SkeletalAnim FromStruct(SkeletalAnimHelper skelAnim)
        {
            SkeletalAnim anim = new SkeletalAnim();
            FromStruct(anim, skelAnim);
            return anim;
        }

        public static SkeletalAnim FromJson(string json)
        {
            SkeletalAnim anim = new SkeletalAnim();
            FromJson(anim, json);
            return anim;
        }

        public static void FromJson(SkeletalAnim anim, string json)
        {
            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings();
                return settings;
            };

            FromStruct(anim, JsonConvert.DeserializeObject<SkeletalAnimHelper>(json));
        }

        public static void FromStruct(SkeletalAnim anim, SkeletalAnimHelper animJson)
        {
            anim.Name = animJson.Name;
            anim.Baked = animJson.Baked;
            anim.Loop = animJson.Loop;
            anim.FrameCount = animJson.FrameCount;
            anim.Baked = animJson.Baked;
            anim.FlagsRotate = animJson.FlagsRotate;
            anim.FlagsScale = animJson.FlagsScale;
            anim.BoneAnims = new List<BoneAnim>();
            anim.BindIndices = new ushort[animJson.BoneAnims.Count];
            anim.UserData = UserDataConvert.Convert(animJson.UserData);

            foreach (var boneAnimJson in animJson.BoneAnims)
            {
                BoneAnim boneAnim = new BoneAnim();
                anim.BoneAnims.Add(boneAnim);

                //Begin offsets depend on whether scale entries are present in the packed data
                boneAnim.Name = boneAnimJson.Name;
                var beginRotate = boneAnimJson.UseBaseScale ? (byte)3 : (byte)0;
                boneAnim.BeginRotate = beginRotate;
                boneAnim.BeginTranslate = (byte)(beginRotate + 3);
                boneAnim.BeginBaseTranslate = (byte)(boneAnim.BeginTranslate + 1);
                Vector4F rotation = boneAnimJson.BaseData.Rotate;
                if (animJson.UseDegrees && animJson.FlagsRotate == SkeletalAnimFlagsRotate.EulerXYZ)
                {
                    rotation = new Vector4F(
                        rotation.X * CurveAnimHelper.Deg2Rad,
                        rotation.Y * CurveAnimHelper.Deg2Rad,
                        rotation.Z * CurveAnimHelper.Deg2Rad,
                        rotation.W);
                }

                boneAnim.BaseData = new BoneAnimData()
                {
                    Flags = boneAnimJson.BaseData.Flags,
                    Rotate = rotation,
                    Translate = boneAnimJson.BaseData.Translate,
                    Scale = boneAnimJson.BaseData.Scale,
                };

                boneAnim.FlagsTransform |= BoneAnimFlagsTransform.Identity;
                if (boneAnimJson.UseBaseTranslation)
                    boneAnim.FlagsBase |= BoneAnimFlagsBase.Translate;
                if (boneAnimJson.UseBaseRotation)
                    boneAnim.FlagsBase |= BoneAnimFlagsBase.Rotate;
                if (boneAnimJson.UseBaseScale)
                    boneAnim.FlagsBase |= BoneAnimFlagsBase.Scale;
                foreach (var curveJson in boneAnimJson.Curves)
                {
                    var target = (AnimTarget)Enum.Parse(typeof(AnimTarget), curveJson.Target);

                    var curve = CurveAnimHelper.GenerateCurve(curveJson, (uint)target,
                        curveJson.Target.Contains("Rotate") && animJson.UseDegrees);
                    boneAnim.Curves.Add(curve);
                    boneAnim.FlagsCurve |= SetCurveTarget(target);
                }
                boneAnim.CalculateTransformFlags();
                boneAnim.ApplySegmentScaleCompensate = boneAnimJson.SegmentScaleCompensate;
            }
        }


        static BoneAnimFlagsCurve SetCurveTarget(AnimTarget target)
        {
            return target switch
            {
                AnimTarget.PositionX => BoneAnimFlagsCurve.TranslateX,
                AnimTarget.PositionY => BoneAnimFlagsCurve.TranslateY,
                AnimTarget.PositionZ => BoneAnimFlagsCurve.TranslateZ,
                AnimTarget.ScaleX => BoneAnimFlagsCurve.ScaleX,
                AnimTarget.ScaleY => BoneAnimFlagsCurve.ScaleY,
                AnimTarget.ScaleZ => BoneAnimFlagsCurve.ScaleZ,
                AnimTarget.RotateX => BoneAnimFlagsCurve.RotateX,
                AnimTarget.RotateY => BoneAnimFlagsCurve.RotateY,
                AnimTarget.RotateZ => BoneAnimFlagsCurve.RotateZ,
                AnimTarget.RotateW => BoneAnimFlagsCurve.RotateW,
                _ => 0,
            };
        }

        public enum AnimTarget
        {
            ScaleX = 0x4,
            ScaleY = 0x8,
            ScaleZ = 0xC,
            PositionX = 0x10,
            PositionY = 0x14,
            PositionZ = 0x18,
            RotateX = 0x20,
            RotateY = 0x24,
            RotateZ = 0x28,
            RotateW = 0x2C,
        }
    }

    public class BoneAnimHelper
    {
        public string Name { get; set; }

        public bool SegmentScaleCompensate { get; set; }

        public bool UseBaseTranslation { get; set; }
        public bool UseBaseRotation { get; set; }
        public bool UseBaseScale { get; set; }

        public List<CurveAnimHelper> Curves { get; set; }

        public BaseDataHelper BaseData { get; set; }
    }

    public struct BaseDataHelper
    {
        public uint Flags;

        public Vector3F Scale;

        public Vector3F Translate;

        public Vector4F Rotate;
    }
}
