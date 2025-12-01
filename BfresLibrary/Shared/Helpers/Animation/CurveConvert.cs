using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using BfresLibrary.TextConvert;

namespace BfresLibrary
{
    public class CurveAnimHelper
    {
        public string Target { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public AnimCurveType Interpolation { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public AnimCurveFrameType FrameType { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public AnimCurveKeyType KeyType { get; set; }

        public string WrapMode { get; set; }

        public float Scale { get; set; }

        [JsonConverter(typeof(DWordJsonConverter))]
        public DWord Offset { get; set; }

        [JsonProperty(ItemConverterType = typeof(NoFormattingConverter))]
        public Dictionary<float, object> KeyFrames { get; set; }

        public static CurveAnimHelper FromCurve(AnimCurve curve, string target, bool useDegrees)
        {
            var convCurve = new CurveAnimHelper();
            convCurve.KeyFrames = new Dictionary<float, object>();
            convCurve.Target = target;
            convCurve.Scale = curve.Scale;
            convCurve.Offset = curve.Offset;
            convCurve.WrapMode = $"{curve.PreWrap}, {curve.PostWrap}";
            convCurve.Interpolation = curve.CurveType;
            convCurve.FrameType = curve.FrameType;
            convCurve.KeyType = curve.KeyType;

            float valueScale = curve.Scale > 0 ? curve.Scale : 1;
            for (int i = 0; i < curve.Frames.Length; i++)
            {
                var frame = curve.Frames[i];
                switch (curve.CurveType)
                {
                    case AnimCurveType.Cubic:
                        {
                            var coef0 = curve.Keys[i, 0] * valueScale + curve.Offset;
                            var slopes = GetSlopes(curve, i);
                            if (useDegrees)
                            {
                                coef0 *= Rad2Deg;
                                slopes[0] *= Rad2Deg;
                                slopes[1] *= Rad2Deg;
                            }

                            convCurve.KeyFrames.Add(frame, new HermiteKey()
                            {
                                Value = coef0,
                                In = slopes[0],
                                Out = slopes[1],
                            });
                        }
                        break;
                    case AnimCurveType.StepBool:
                        convCurve.KeyFrames.Add(frame, new BooleanKey()
                        {
                            Value = curve.KeyStepBoolData[i],
                        });
                        break;
                    case AnimCurveType.StepInt:
                        convCurve.KeyFrames.Add(frame, new KeyFrame()
                        {
                            Value = (int)curve.Keys[i, 0] + (int)curve.Offset
                        });
                        break;
                    case AnimCurveType.Linear:
                        {
                            var value = curve.Keys[i, 0] * valueScale + curve.Offset;
                            if (useDegrees)
                                value *= Rad2Deg;

                            convCurve.KeyFrames.Add(frame, new LinearKeyFrame()
                            {
                                Value = value,
                                Delta = curve.Keys[i, 1] * valueScale,
                            });
                        }
                        break;
                    default:
                        {
                            var value = curve.Keys[i, 0] * valueScale + curve.Offset;
                            if (useDegrees)
                                value *= Rad2Deg;

                            convCurve.KeyFrames.Add(frame, new KeyFrame()
                            {
                                Value = value
                            });
                        }
                        break;
                }
            }
            return convCurve;
        }

        public static AnimCurve GenerateCurve(CurveAnimHelper curveJson, uint target, bool isDegrees)
        {
            AnimCurve curve = new AnimCurve();
            curve.Offset = curveJson.Offset;
            curve.Scale = curveJson.Scale;
            curve.CurveType = curveJson.Interpolation;
            curve.FrameType = curveJson.FrameType;
            curve.KeyType = curveJson.KeyType;
            curve.AnimDataOffset = target;

            var first = curveJson.KeyFrames.First();
            var last = curveJson.KeyFrames.Last();
            curve.EndFrame = last.Key;
            curve.StartFrame = first.Key;

            var keys = curveJson.KeyFrames.Values.ToList();
            var frames = curveJson.KeyFrames.Keys.ToList();
            curve.Frames = frames.ToArray();
            curve.KeyStepBoolData = curveJson.KeyFrames.Select(x => ToObject<BooleanKey>(x.Value).Value).ToArray();
            curve.Keys = new float[keys.Count, 1];
            if (curve.CurveType == AnimCurveType.Cubic) curve.Keys = new float[keys.Count, 4];
            if (curve.CurveType == AnimCurveType.Linear) curve.Keys = new float[keys.Count, 2];

            //type overrides for santity check
            var maxFrame = curve.Frames.Max(x => x);
            if (maxFrame > 255 && curve.FrameType == AnimCurveFrameType.Byte)
                curve.FrameType = AnimCurveFrameType.Decimal10x5;
            if (maxFrame > ushort.MaxValue)
                curve.FrameType = AnimCurveFrameType.Single;

            for (int i = 0; i < keys.Count; i++)
            {
                switch (curve.CurveType)
                {
                    case AnimCurveType.Cubic:
                        var hermiteKey = ToObject<HermiteKey>(keys[i]);

                        float time = 0;
                        float value = hermiteKey.Value;
                        float outSlope = hermiteKey.Out;
                        float nextValue = 0;
                        float nextInSlope = 0;
                        if (i < keys.Count - 1)
                        {
                            var nextKey = ToObject<HermiteKey>(keys[i + 1]);
                            var nextFrame = frames[i + 1];

                            nextValue = nextKey.Value;
                            nextInSlope = nextKey.In;
                            time = nextFrame - frames[i];
                        }
                        if (isDegrees)
                        {
                            value *= Deg2Rad;
                            nextValue *= Deg2Rad;
                            nextInSlope *= Deg2Rad;
                            outSlope *= Deg2Rad;
                        }

                        float[] coefs = HermiteToCubicKey(
                            value, nextValue,
                            outSlope * time, nextInSlope * time);

                        curve.Keys[i, 0] = coefs[0];
                        if (time != 0)
                        {
                            curve.Keys[i, 1] = coefs[1];
                            curve.Keys[i, 2] = coefs[2];
                            curve.Keys[i, 3] = coefs[3];
                        }
                        break;
                    case AnimCurveType.StepBool:
                        var booleanKey = ToObject<BooleanKey>(keys[i]);
                        curve.KeyStepBoolData[i] = booleanKey.Value;
                        break;
                    case AnimCurveType.Linear:
                        if (keys[i] is LinearKeyFrame)
                        {
                            var linearKey = ToObject<LinearKeyFrame>(keys[i]);
                            float linearValue = linearKey.Value;
                            if (isDegrees)
                            {
                                linearValue *= Deg2Rad;
                            }
                            curve.Keys[i, 0] = linearValue;
                            curve.Keys[i, 1] = linearKey.Delta;
                        }
                        else
                        {
                            var linearKey = ToObject<KeyFrame>(keys[i]);
                            float linearValue = linearKey.Value;
                            if (isDegrees)
                            {
                                linearValue *= Deg2Rad;
                            }

                            float delta = 0;

                            if (i < keys.Count - 1)
                            {
                                var nextKey = ToObject<KeyFrame>(keys[i + 1]);
                                float nextLinearValue = nextKey.Value;
                                if (isDegrees)
                                    nextLinearValue *= Deg2Rad;

                                delta = nextLinearValue - linearValue;
                            }

                            curve.Keys[i, 0] = linearValue;
                            curve.Keys[i, 1] = delta;
                        }
                        break;
                    case AnimCurveType.StepInt:
                        var stepKey = ToObject<KeyFrame>(keys[i]);
                        curve.Keys[i, 0] = (int)stepKey.Value - (int)curveJson.Offset;
                        break;
                }
            }

            if (curve.Keys.Length >= 2)
            {
                var lastKey = curve.Keys[keys.Count - 1, 0];
                var firstKey = curve.Keys[0, 0];

                curve.Delta = lastKey - firstKey;
            }

            float minQuantized = float.MaxValue;
            float maxQuantized = float.MinValue;

            for (int i = 0; i < keys.Count; i++)
            {
                curve.Keys[i, 0] -= curve.Offset;

                int elementsToCheck = 1;

                //Apply scale for cubic and linear curves only
                if (curve.CurveType == AnimCurveType.Cubic)
                {
                    elementsToCheck = 4;
                    if (curve.Scale != 0)
                    {
                        curve.Keys[i, 0] /= curve.Scale;
                        curve.Keys[i, 1] /= curve.Scale;
                        curve.Keys[i, 2] /= curve.Scale;
                        curve.Keys[i, 3] /= curve.Scale;
                    }
                }
                else if (curve.CurveType == AnimCurveType.Linear)
                {
                    elementsToCheck = 2;
                    if (curve.Scale != 0)
                    {
                        curve.Keys[i, 0] /= curve.Scale;
                        curve.Keys[i, 1] /= curve.Scale;
                    }
                }

                // Track min/max values to detect SByte overflow
                for (int k = 0; k < elementsToCheck; k++)
                {
                    float val = curve.Keys[i, k];
                    if (val < minQuantized) minQuantized = val;
                    if (val > maxQuantized) maxQuantized = val;
                }
            }

            // Check if the calculated values fit into SByte (-128 to 127)
            // We add a small buffer (0.5) to account for float rounding errors before casting
            if (curve.KeyType == AnimCurveKeyType.SByte)
            {
                if (minQuantized < -128.5f || maxQuantized > 127.5f)
                {
                    // SByte Overflow detected! Upgrade to Int16.
                    // Console.WriteLine($"[Fix] Upgrading {target} curve from SByte to Int16 due to range [{minQuantized} to {maxQuantized}]");
                    curve.KeyType = AnimCurveKeyType.Int16;
                }
            }

            // Optional: Check if it fits into Int16 (-32768 to 32767)
            // If not, you might need to upgrade to Single, though Scale/Offset usually prevents this for Int16.
            if (curve.KeyType == AnimCurveKeyType.Int16)
            {
                if (minQuantized < -32768.5f || maxQuantized > 32767.5f)
                {
                    // Int16 Overflow detected! Upgrade to Float (Single)
                    curve.KeyType = AnimCurveKeyType.Single;
                    // Note: If upgrading to Single, Offset and Scale usually become 0 and 1,
                    // but dealing with that requires undoing the math above.
                    // Usually Int16 is sufficient for this file format.
                }
            }

            return curve;
        }

        static T ToObject<T>(object obj)
        {
            if (obj is JObject) return ((JObject)obj).ToObject<T>();
            else
                return (T)obj;
        }

        public static float Rad2Deg = (float)(360 / (System.Math.PI * 2));
        public static float Deg2Rad = (float)(System.Math.PI * 2) / 360;

        public static float[] HermiteToCubicKey(float p0, float p1, float s0, float s1)
        {
            float[] coefs = new float[4];
            coefs[3] = (p0 * 2) + (p1 * -2) + (s0 * 1) + (s1 * 1);
            coefs[2] = (p0 * -3) + (p1 * 3) + (s0 * -2) + (s1 * -1);
            coefs[1] = (p0 * 0) + (p1 * 0) + (s0 * 1) + (s1 * 0);
            coefs[0] = (p0 * 1) + (p1 * 0) + (s0 * 0) + (s1 * 0);
            return coefs;
        }

        //Method to extract the slopes from a cubic curve
        //Need to get the time, delta, out and next in slope values
        public static float[] GetSlopes(AnimCurve curve, float index)
        {
            float[] slopes = new float[2];
            if (curve.CurveType == AnimCurveType.Cubic)
            {
                float InSlope = 0;
                float OutSlope = 0;
                for (int i = 0; i < curve.Frames.Length; i++)
                {
                    var coef0 = curve.Keys[i, 0] * curve.Scale + curve.Offset;
                    var coef1 = curve.Keys[i, 1] * curve.Scale;
                    var coef2 = curve.Keys[i, 2] * curve.Scale;
                    var coef3 = curve.Keys[i, 3] * curve.Scale;
                    float time = 0;
                    float delta = 0;
                    if (i < curve.Frames.Length - 1)
                    {
                        var nextValue = curve.Keys[i + 1, 0] * curve.Scale + curve.Offset;
                        delta = nextValue - coef0;
                        time = curve.Frames[i + 1] - curve.Frames[i];
                    }

                    var slopeData = GetCubicSlopes(time, delta,
                        new float[4] { coef0, coef1, coef2, coef3, });

                    if (index == i)
                    {
                        OutSlope = slopeData[1];
                        return new float[2] { InSlope, OutSlope };
                    }

                    //The previous inslope is used
                    InSlope = slopeData[0];
                }
            }

            return slopes;
        }

        public static float[] GetCubicSlopes(float time, float delta, float[] coef)
        {
            float outSlope = coef[1] / time;
            float param = coef[3] - (-2 * delta);
            float inSlope = param / time - outSlope;
            return new float[2] { inSlope, coef[1] == 0 ? 0 : outSlope };
        }
    }

    public class HermiteKey
    {
        public float Value { get; set; }
        public float In { get; set; }
        public float Out { get; set; }
    }

    public class BooleanKey
    {
        public bool Value { get; set; }
    }

    public class LinearKeyFrame
    {
        public float Value { get; set; }
        public float Delta { get; set; }
    }

    public class KeyFrame
    {
        public float Value { get; set; }
    }
}
