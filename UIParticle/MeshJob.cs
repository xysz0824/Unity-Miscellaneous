using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using RandomNative = Unity.Mathematics.Random;
using Unity.Mathematics;

namespace Coffee.UIExtensions
{
    public struct MinMaxGradientNative
    {
        static readonly GradientColorKey[] emptyGradientColorKeys = new GradientColorKey[0];

        public ParticleSystemGradientMode psGrandientMode;
        public GradientMode gradientMode;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<GradientColorKey> gradientKeys;
        public GradientMode minGradientMode;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<GradientColorKey> minGradientKeys;
        public GradientMode maxGradientMode;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<GradientColorKey> maxGradientKeys;
        public Color color;
        public Color colorMin;
        public Color colorMax;
        public void Clone(ref NativeArray<GradientColorKey> keys, Gradient gradient)
        {
            Array colorKeys = emptyGradientColorKeys;
            if (gradient != null)
            {
                if (gradient.alphaKeys.Length > gradient.colorKeys.Length) colorKeys = gradient.alphaKeys;
                else colorKeys = gradient.colorKeys;
            }
            if (!keys.IsCreated || keys.Length != colorKeys.Length)
            {
                if (keys.IsCreated) keys.Dispose();
                keys = new NativeArray<GradientColorKey>(colorKeys.Length, Allocator.Persistent);
            }
            for (int i = 0; i < colorKeys.Length; ++i)
            {
                var time = colorKeys is GradientColorKey[]? gradient.colorKeys[i].time : gradient.alphaKeys[i].time;
                keys[i] = new GradientColorKey(gradient.Evaluate(time), time);
            }
        }
        public void CopyFrom(ParticleSystem.MinMaxGradient minMaxGradient)
        {
            psGrandientMode = minMaxGradient.mode;
            color = minMaxGradient.color;
            colorMin = minMaxGradient.colorMin;
            colorMax = minMaxGradient.colorMax;
            if (minMaxGradient.gradient != null) gradientMode = minMaxGradient.gradient.mode;
            Clone(ref gradientKeys, minMaxGradient.gradient);
            if (minMaxGradient.gradientMin != null) minGradientMode = minMaxGradient.gradientMin.mode;
            Clone(ref minGradientKeys, minMaxGradient.gradientMin);
            if (minMaxGradient.gradientMax != null) maxGradientMode = minMaxGradient.gradientMax.mode;
            Clone(ref maxGradientKeys, minMaxGradient.gradientMax);
        }
        public Color Evaluate(ref NativeArray<GradientColorKey> keys, GradientMode mode, float time)
        {
            if (keys.Length == 0) return Color.black;
            for (int i = 0; i < keys.Length - 1; ++i)
            {
                if (time >= keys[i].time && time <= keys[i + 1].time)
                {
                    if (mode == GradientMode.Fixed) return keys[i].color;
                    time = (time - keys[i].time) / (keys[i + 1].time - keys[i].time);
                    return Color.Lerp(keys[i].color, keys[i + 1].color, time);
                }
            }
            if (time <= 0) return keys[0].color;
            else return keys[keys.Length - 1].color;
        }
        public Color Evaluate(float time, ref RandomNative rand)
        {
            switch (psGrandientMode)
            {
                case ParticleSystemGradientMode.TwoColors:
                    return Color.Lerp(colorMin, colorMax, rand.NextFloat());
                case ParticleSystemGradientMode.Gradient:
                    return Evaluate(ref gradientKeys, gradientMode, time);
                case ParticleSystemGradientMode.TwoGradients:
                    return Color.Lerp(Evaluate(ref minGradientKeys, minGradientMode, time), Evaluate(ref maxGradientKeys, maxGradientMode, time), rand.NextFloat());
                default:
                    return color;
            }
        }
        public void Dispose()
        {
            if (gradientKeys.IsCreated)
            {
                gradientKeys.Dispose();
                gradientKeys = default;
            }
            if (minGradientKeys.IsCreated)
            {
                minGradientKeys.Dispose();
                minGradientKeys = default;
            }
            if (maxGradientKeys.IsCreated)
            {
                maxGradientKeys.Dispose();
                maxGradientKeys = default;
            }
        }
    }
    public struct MinMaxCurveNative
    {
        static readonly Keyframe[] emptyKeyFrames = new Keyframe[0];

        public ParticleSystemCurveMode curveMode;
        public float curveMultiplier;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Keyframe> curveKeys;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Keyframe> minCurveKeys;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Keyframe> maxCurveKeys;
        public float constant;
        public float constantMin;
        public float constantMax;
        public void Clone(ref NativeArray<Keyframe> keys, AnimationCurve curve)
        {
            var keyFrames = curve != null ? curve.keys : emptyKeyFrames;
            if (!keys.IsCreated || keys.Length != keyFrames.Length)
            {
                if (keys.IsCreated) keys.Dispose();
                keys = new NativeArray<Keyframe>(keyFrames.Length, Allocator.Persistent);
            }
            keys.CopyFrom(keyFrames);
        }
        public void CopyFrom(ParticleSystem.MinMaxCurve minMaxCurve)
        {
            curveMode = minMaxCurve.mode;
            curveMultiplier = minMaxCurve.curveMultiplier;
            Clone(ref curveKeys, minMaxCurve.curve);
            Clone(ref minCurveKeys, minMaxCurve.curveMin);
            Clone(ref maxCurveKeys, minMaxCurve.curveMax);
            constant = minMaxCurve.constant;
            constantMin = minMaxCurve.constantMin;
            constantMax = minMaxCurve.constantMax;
        }
        static float HermiteInterpolate(float curveT, Keyframe lhs, Keyframe rhs)
        {
            float dx = rhs.time - lhs.time;
            float m1;
            float m2;
            float t;
            if (dx != 0.0F)
            {
                t = (curveT - lhs.time) / dx;
                m1 = lhs.outTangent * dx;
                m2 = rhs.inTangent * dx;
            }
            else
            {
                t = 0.0F;
                m1 = 0;
                m2 = 0;
            }
            return HermiteInterpolate(t, lhs.value, m1, m2, rhs.value);
        }

        static float HermiteInterpolate(float t, float p0, float m0, float m1, float p1)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float a = 2.0F * t3 - 3.0F * t2 + 1.0F;
            float b = t3 - 2.0F * t2 + t;
            float c = t3 - t2;
            float d = -2.0F * t3 + 3.0F * t2;

            return a * p0 + b * m0 + c * m1 + d * p1;
        }
        public float Evaluate(ref NativeArray<Keyframe> keys, float time)
        {
            if (keys.Length == 0) return 0;
            for (int i = 0; i < keys.Length - 1; ++i)
            {
                if (time >= keys[i].time && time <= keys[i + 1].time)
                {
                    return HermiteInterpolate(time, keys[i], keys[i + 1]);
                }
            }
            if (time <= 0) return keys[0].value;
            else return keys[keys.Length - 1].value;
        }
        public float Evaluate(float time, ref RandomNative rand)
        {
            switch (curveMode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Lerp(constantMin, constantMax, rand.NextFloat());
                case ParticleSystemCurveMode.Curve:
                    return Evaluate(ref curveKeys, time);
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Lerp(Evaluate(ref minCurveKeys, time), Evaluate(ref maxCurveKeys, time), rand.NextFloat());
                default:
                    return constant;
            }
        }
        public void Dispose()
        {
            if (curveKeys.IsCreated)
            {
                curveKeys.Dispose();
                curveKeys = default;
            }
            if (minCurveKeys.IsCreated) 
            {
                minCurveKeys.Dispose();
                minCurveKeys = default;
            }

            if (maxCurveKeys.IsCreated) 
            {
                maxCurveKeys.Dispose();
                maxCurveKeys = default;
            }        }
    }
    public struct ColorOverLifeTime
    {
        public bool enabled;
        public MinMaxGradientNative color;
        public void CopyFrom(ParticleSystem.ColorOverLifetimeModule module)
        {
            enabled = module.enabled;
            color.CopyFrom(module.color);
        }
        public Color Evaluate(float time, ref RandomNative rand)
        {
            return color.Evaluate(time, ref rand);
        }
        public void Dispose() => color.Dispose();
    }
    public struct ColorBySpeed
    {
        public ColorOverLifeTime color;
        public Vector2 range;
        public void CopyFrom(ParticleSystem.ColorBySpeedModule module)
        {
            color.enabled = module.enabled;
            color.color.CopyFrom(module.color);
            range = module.range;
        }
        public Color Evaluate(float velocity, ref RandomNative rand)
        {
            var time = Mathf.InverseLerp(range.x, range.y, velocity);
            return color.Evaluate(time, ref rand);
        }
        public void Dispose() => color.Dispose();
    }
    public struct SizeOverLifeTime
    {
        public bool enabled;
        public MinMaxCurveNative size;
        public float sizeMultiplier;
        public MinMaxCurveNative x;
        public float xMultiplier;
        public MinMaxCurveNative y;
        public float yMultiplier;
        public MinMaxCurveNative z;
        public float zMultiplier;
        public bool separateAxes;
        public void CopyFrom(ParticleSystem.SizeOverLifetimeModule module)
        {
            enabled = module.enabled;
            size.CopyFrom(module.size);
            sizeMultiplier = module.sizeMultiplier;
            x.CopyFrom(module.x);
            xMultiplier = module.xMultiplier;
            y.CopyFrom(module.y);
            yMultiplier = module.yMultiplier;
            z.CopyFrom(module.z);
            zMultiplier = module.zMultiplier;
            separateAxes = module.separateAxes;
        }
        public Vector3 Evaluate(float time, ref RandomNative rand)
        {
            if (!separateAxes) return sizeMultiplier * size.Evaluate(time, ref rand) * new Vector3(1, 1, 1);
            else
            {
                var result = new Vector3();
                result.x = xMultiplier * x.Evaluate(time, ref rand);
                result.y = yMultiplier * y.Evaluate(time, ref rand);
                result.z = zMultiplier * z.Evaluate(time, ref rand);
                return result;
            }
        }
        public void Dispose()
        {
            size.Dispose();
            x.Dispose();
            y.Dispose();
            z.Dispose();
        }
    }
    public struct SizeBySpeed
    {
        public SizeOverLifeTime size;
        public Vector2 range;
        public void CopyFrom(ParticleSystem.SizeBySpeedModule module)
        {
            size.enabled = module.enabled;
            size.size.CopyFrom(module.size);
            size.sizeMultiplier = module.sizeMultiplier;
            size.x.CopyFrom(module.x);
            size.xMultiplier = module.xMultiplier;
            size.y.CopyFrom(module.y);
            size.yMultiplier = module.yMultiplier;
            size.z.CopyFrom(module.z);
            size.zMultiplier = module.zMultiplier;
            size.separateAxes = module.separateAxes;
            range = module.range;
        }
        public Vector3 Evaluate(float velocity, ref RandomNative rand)
        {
            var time = Mathf.InverseLerp(range.x, range.y, velocity);
            return size.Evaluate(time, ref rand);
        }
        public void Dispose() => size.Dispose();
    }
    public struct TextureSheetAnimation
    {
        public bool enabled;
        public int numTilesX;
        public int numTilesY;
        public ParticleSystemAnimationType type;
        public ParticleSystemAnimationRowMode rowMode;
        public int rowIndex;
        public ParticleSystemAnimationTimeMode timeMode;
        public float frameOverTimeMultiplier;
        public MinMaxCurveNative frameOverTime;
        public Vector2 speedRange;
        public float fps;
        public float startFrameMultiplier;
        public MinMaxCurveNative startFrame;
        public int cycleCount;
        public void CopyFrom(ParticleSystem.TextureSheetAnimationModule module)
        {
            enabled = module.enabled;
            numTilesX = module.numTilesX;
            numTilesY = module.numTilesY;
            type = module.animation;
            rowMode = module.rowMode;
            rowIndex = module.rowIndex;
            timeMode = module.timeMode;
            frameOverTimeMultiplier = module.frameOverTimeMultiplier;
            frameOverTime.CopyFrom(module.frameOverTime);
            speedRange = module.speedRange;
            fps = module.fps;
            startFrameMultiplier = module.startFrameMultiplier;
            startFrame.CopyFrom(module.startFrame);
            cycleCount = module.cycleCount;
        }
        public Vector2 GetScale() => new Vector2(1.0f / numTilesX, 1.0f / numTilesY);
        public Vector2 GetOffset(float timeNormalized, float timeSeconds, float speed, ref RandomNative rand)
        {
            int startTile = 0;
            int endTile = 0;
            if (type == ParticleSystemAnimationType.WholeSheet)
            {
                endTile = numTilesX * numTilesY;
            }
            else
            {
                switch (rowMode)
                {
                    case ParticleSystemAnimationRowMode.Custom:
                        startTile = rowIndex * numTilesX;
                        break;
                    case ParticleSystemAnimationRowMode.Random:
                        startTile = rand.NextInt(0, numTilesY) * numTilesX;
                        break;
                    case ParticleSystemAnimationRowMode.MeshIndex:
                        startTile = 0;
                        break;
                }
                endTile = startTile + numTilesX;
            }
            int tileCount = endTile - startTile;
            int startTileOffset = (int)(startFrameMultiplier * startFrame.Evaluate(0, ref rand) * tileCount);
            int tile = 0;
            if (timeMode == ParticleSystemAnimationTimeMode.Lifetime)
            {
                tile = (int)(frameOverTimeMultiplier * frameOverTime.Evaluate(timeNormalized, ref rand) * tileCount);
            }
            else if (timeMode == ParticleSystemAnimationTimeMode.Speed)
            {
                tile = (int)(Mathf.InverseLerp(speedRange.x, speedRange.y, speed) * tileCount);
            }
            else if (timeMode == ParticleSystemAnimationTimeMode.FPS)
            {
                tile = (int)(fps * timeSeconds);
            }
            tile = startTile + (tile + startTileOffset) % tileCount;
            return new Vector2((tile % (float)numTilesX) / numTilesX, 1.0f - (1 + tile / numTilesX) / (float)numTilesY);
        }
        public void Dispose()
        {
            frameOverTime.Dispose();
            startFrame.Dispose();
        }
    }
    public struct CustomData
    {
        public bool enabled;
        public ParticleSystemCustomDataMode custom1Mode;
        public int custom1ComponentCount;
        public MinMaxCurveNative custom1VectorCurveX;
        public MinMaxCurveNative custom1VectorCurveY;
        public MinMaxCurveNative custom1VectorCurveZ;
        public MinMaxCurveNative custom1VectorCurveW;
        public MinMaxGradientNative custom1ColorGradient;
        public ParticleSystemCustomDataMode custom2Mode;
        public int custom2ComponentCount;
        public MinMaxCurveNative custom2VectorCurveX;
        public MinMaxCurveNative custom2VectorCurveY;
        public MinMaxCurveNative custom2VectorCurveZ;
        public MinMaxCurveNative custom2VectorCurveW;
        public MinMaxGradientNative custom2ColorGradient;
        public void CopyFrom(ParticleSystem.CustomDataModule module)
        {
            enabled = module.enabled;
            custom1Mode = module.GetMode(ParticleSystemCustomData.Custom1);
            custom1ComponentCount = custom1Mode == ParticleSystemCustomDataMode.Vector ? module.GetVectorComponentCount(ParticleSystemCustomData.Custom1) : 4;
            if (custom1Mode == ParticleSystemCustomDataMode.Vector)
            {
                custom1VectorCurveX.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom1, 0));
                if (custom1ComponentCount >= 2) custom1VectorCurveY.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom1, 1));
                if (custom1ComponentCount >= 3) custom1VectorCurveZ.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom1, 2));
                if (custom1ComponentCount >= 4) custom1VectorCurveW.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom1, 3));
            }
            else custom1ColorGradient.CopyFrom(module.GetColor(ParticleSystemCustomData.Custom1));
            custom2Mode = module.GetMode(ParticleSystemCustomData.Custom2);
            custom2ComponentCount = custom2Mode == ParticleSystemCustomDataMode.Vector ? module.GetVectorComponentCount(ParticleSystemCustomData.Custom2) : 4;
            if (custom2Mode == ParticleSystemCustomDataMode.Vector)
            {
                custom2VectorCurveX.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom2, 0));
                if (custom2ComponentCount >= 2) custom2VectorCurveY.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom2, 1));
                if (custom2ComponentCount >= 3) custom2VectorCurveZ.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom2, 2));
                if (custom2ComponentCount >= 4) custom2VectorCurveW.CopyFrom(module.GetVector(ParticleSystemCustomData.Custom2, 3));
            }
            else custom2ColorGradient.CopyFrom(module.GetColor(ParticleSystemCustomData.Custom2));
        }
        public Vector4 GetCustom1(float time, ref RandomNative rand)
        {
            switch (custom1Mode)
            {
                case ParticleSystemCustomDataMode.Vector:
                    float x = custom1VectorCurveX.Evaluate(time, ref rand);
                    float y = custom1ComponentCount >= 2 ? custom1VectorCurveY.Evaluate(time, ref rand) : 0;
                    float z = custom1ComponentCount >= 3 ? custom1VectorCurveZ.Evaluate(time, ref rand) : 0;
                    float w = custom1ComponentCount >= 4 ? custom1VectorCurveW.Evaluate(time, ref rand) : 0;
                    return new Vector4(x, y, z, w);
                case ParticleSystemCustomDataMode.Color:
                    return custom1ColorGradient.Evaluate(time, ref rand);
                default:
                    return new Vector4();
            }
        }
        public Vector4 GetCustom2(float time, ref RandomNative rand)
        {
            switch (custom2Mode)
            {
                case ParticleSystemCustomDataMode.Vector:
                    float x = custom2VectorCurveX.Evaluate(time, ref rand);
                    float y = custom2ComponentCount >= 2 ? custom2VectorCurveY.Evaluate(time, ref rand) : 0;
                    float z = custom2ComponentCount >= 3 ? custom2VectorCurveZ.Evaluate(time, ref rand) : 0;
                    float w = custom2ComponentCount >= 4 ? custom2VectorCurveW.Evaluate(time, ref rand) : 0;
                    return new Vector4(x, y, z, w);
                case ParticleSystemCustomDataMode.Color:
                    return custom2ColorGradient.Evaluate(time, ref rand);
                default:
                    return new Vector4();
            }
        }
        public void Dispose()
        {
            custom1VectorCurveX.Dispose();
            custom1VectorCurveY.Dispose();
            custom1VectorCurveZ.Dispose();
            custom1VectorCurveW.Dispose();
            custom1ColorGradient.Dispose();
            custom2VectorCurveX.Dispose();
            custom2VectorCurveY.Dispose();
            custom2VectorCurveZ.Dispose();
            custom2VectorCurveW.Dispose();
            custom2ColorGradient.Dispose();
        }
    }
    public struct CustomVertexStreams
    {
        static int[] streamLengthSheet = { 0, 0, 0, 0, 2, 2, 2, 2, 1, 1, 3, 1, 1, 2, 3, 1, 3, 1, 3, 3, 1, 1, 1, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 1, 2, 3, 1 };
        static List<ParticleSystemVertexStream> activeVertexStreams = new List<ParticleSystemVertexStream>();
        public bool custom1Active;
        public int custom1ChannelStart;
        public int custom1ChannelEnd;
        public bool custom2Active;
        public int custom2ChannelStart;
        public int custom2ChannelEnd;
        public void CopyFrom(ParticleSystemRenderer renderer)
        {
            renderer.GetActiveVertexStreams(activeVertexStreams);
            int offset = 0;
            custom1Active = custom2Active = false;
            for (int i = 0;i < activeVertexStreams.Count; ++i)
            {
                var length = streamLengthSheet[(int)activeVertexStreams[i]];
                if (activeVertexStreams[i] >= ParticleSystemVertexStream.Custom1X && activeVertexStreams[i] <= ParticleSystemVertexStream.Custom1XYZW)
                {
                    custom1Active = true;
                    custom1ChannelStart = offset;
                    custom1ChannelEnd = offset + length;
                }
                if (activeVertexStreams[i] >= ParticleSystemVertexStream.Custom2X && activeVertexStreams[i] <= ParticleSystemVertexStream.Custom2XYZW)
                {
                    custom2Active = true;
                    custom2ChannelStart = offset;
                    custom2ChannelEnd = offset + length;
                }
                offset += length;
            }
        }
        public Vector4 GetTexcoord(Vector4 v, int texcoordStart, ref Vector4 custom1, ref Vector4 custom2)
        {
            if (custom1Active)
            {
                int start = Mathf.Clamp(custom1ChannelStart - texcoordStart, 0, 4);
                int end = Mathf.Min(custom1ChannelEnd - texcoordStart, 4);
                int offset = Mathf.Max(texcoordStart - custom1ChannelStart, 0);
                for (int i = start; i < end; ++i)
                {
                    v[i] = custom1[i - start + offset];
                }
            }
            if (custom2Active)
            {
                int start = Mathf.Clamp(custom2ChannelStart - texcoordStart, 0, 4);
                int end = Mathf.Min(custom2ChannelEnd - texcoordStart, 4);
                int offset = Mathf.Max(texcoordStart - custom2ChannelStart, 0);
                for (int i = start; i < end; ++i)
                {
                    v[i] = custom2[i - start + offset];
                }
            }
            return v;
        }
        public Vector4 GetTexcoord0(Vector4 v, ref Vector4 custom1, ref Vector4 custom2)
        {
            return GetTexcoord(v, 0, ref custom1, ref custom2);
        }
        public Vector4 GetTexcoord1(ref Vector4 custom1, ref Vector4 custom2)
        {
            return GetTexcoord(new Vector4(), 4, ref custom1, ref custom2);
        }
    }
    public struct ParticleSystemNative
    {
        public ColorOverLifeTime colorOverLifeTime;
        public ColorBySpeed colorBySpeed;
        public SizeOverLifeTime sizeOverLifeTime;
        public SizeBySpeed sizeBySpeed;
        public TextureSheetAnimation textureSheetAnimation;
        public CustomData customData;
        public ParticleSystemRenderMode renderMode;
        public Vector3 flip;
        public Vector3 pivot;
        public float speedScale;
        public float lengthScale;
        public CustomVertexStreams customVertexStreams;
        public void CopyFrom(ParticleSystem particleSystem, ParticleSystemRenderer renderer)
        {
            colorOverLifeTime.CopyFrom(particleSystem.colorOverLifetime);
            colorBySpeed.CopyFrom(particleSystem.colorBySpeed);
            sizeOverLifeTime.CopyFrom(particleSystem.sizeOverLifetime);
            sizeBySpeed.CopyFrom(particleSystem.sizeBySpeed);
            textureSheetAnimation.CopyFrom(particleSystem.textureSheetAnimation);
            customData.CopyFrom(particleSystem.customData);

            renderMode = renderer.renderMode;
            flip = renderer.flip;
            pivot = renderer.pivot;
            speedScale = renderer.velocityScale;
            lengthScale = renderer.lengthScale;
            customVertexStreams.CopyFrom(renderer);
        }
        public void Dispose()
        {
            colorOverLifeTime.Dispose();
            colorBySpeed.Dispose();
            sizeOverLifeTime.Dispose();
            sizeBySpeed.Dispose();
            textureSheetAnimation.Dispose();
            customData.Dispose();
        }
    }
    [BurstCompile]
    struct MeshJob : IJobParallelFor
    {
        public Matrix4x4 matrix;
        public Matrix4x4 alignMatrix;
        public Matrix4x4 scaleMatrix;
        public ParticleSystemNative particleSystemNative;
        [ReadOnly]
        public NativeArray<ParticleSystem.Particle> particles;
        public int particleIndex;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector3> vertices;
        public int vertexBase;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Color> colors;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector4> texcoord0;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector4> texcoord1;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> indices;
        public int indexOffset;
        public void Execute(int i)
        {
            int psIndex = particleIndex + i;
            Vector4 posCenter = new Vector4(particles[psIndex].position.x, particles[psIndex].position.y, particles[psIndex].position.z, 1);
            Quaternion rotation = new Quaternion();
            if (particleSystemNative.renderMode == ParticleSystemRenderMode.Stretch)
            {
                rotation = Quaternion.FromToRotation(new Vector3(-1, 0, 0), particles[psIndex].totalVelocity);
            }
            else
            {
                rotation = Quaternion.Euler(Vector3.Scale(new Vector3(1, 1, -1), particles[psIndex].rotation3D));
            }
            float timeSeconds = particles[psIndex].startLifetime - particles[psIndex].remainingLifetime;
            float timeNormalized = timeSeconds / particles[psIndex].startLifetime;
            float speed = particles[psIndex].totalVelocity.magnitude;
            var rand = new RandomNative(particles[psIndex].randomSeed);
            Vector3 size = particles[psIndex].startSize3D;
            if (particleSystemNative.sizeOverLifeTime.enabled)
            {
                size = Vector3.Scale(size, particleSystemNative.sizeOverLifeTime.Evaluate(timeNormalized, ref rand));
            }
            if (particleSystemNative.sizeBySpeed.size.enabled)
            {
                size = Vector3.Scale(size, particleSystemNative.sizeBySpeed.Evaluate(speed, ref rand));
            }
            Vector3 positionLB = new Vector3(-0.5f, -0.5f, 0);
            Vector3 positionLT = new Vector3(-0.5f, 0.5f, 0);
            Vector3 positionRT = new Vector3(0.5f, 0.5f, 0);
            Vector3 positionRB = new Vector3(0.5f, -0.5f, 0);
            if (particleSystemNative.renderMode == ParticleSystemRenderMode.Stretch)
            {
                var temp = size.x;
                size.x = size.y;
                size.y = temp;
                size.x *= particleSystemNative.lengthScale;
                size.x += speed * particleSystemNative.speedScale;
                positionLB.x += 0.5f;
                positionLT.x += 0.5f;
                positionRT.x += 0.5f;
                positionRB.x += 0.5f;
            }
            positionLB = rotation * Vector3.Scale(size, positionLB + particleSystemNative.pivot);
            positionLT = rotation * Vector3.Scale(size, positionLT + particleSystemNative.pivot);
            positionRT = rotation * Vector3.Scale(size, positionRT + particleSystemNative.pivot);
            positionRB = rotation * Vector3.Scale(size, positionRB + particleSystemNative.pivot);
            int vertexIndex = vertexBase + i * 4;
            var finalMatrix = scaleMatrix * matrix;
            vertices[vertexIndex] = finalMatrix * (posCenter + alignMatrix * positionLB);
            vertices[vertexIndex + 1] = finalMatrix * (posCenter + alignMatrix * positionLT);
            vertices[vertexIndex + 2] = finalMatrix * (posCenter + alignMatrix * positionRT);
            vertices[vertexIndex + 3] = finalMatrix * (posCenter + alignMatrix * positionRB);
            Color color = particles[psIndex].startColor;
            if (particleSystemNative.colorOverLifeTime.enabled)
            {
                color *= particleSystemNative.colorOverLifeTime.Evaluate(timeNormalized, ref rand);
            }
            if (particleSystemNative.colorBySpeed.color.enabled)
            {
                color *= particleSystemNative.colorBySpeed.Evaluate(speed, ref rand);
            }
            //if (colorSpace == ColorSpace.Linear) color = color.gamma;
            colors[vertexIndex] = colors[vertexIndex + 1] = colors[vertexIndex + 2] = colors[vertexIndex + 3] = color;
            Vector2 uvScale = new Vector2(1, 1);
            Vector2 uvOffset = new Vector2(0, 0);
            if (particleSystemNative.textureSheetAnimation.enabled)
            {
                uvScale = particleSystemNative.textureSheetAnimation.GetScale();
                uvOffset = particleSystemNative.textureSheetAnimation.GetOffset(timeNormalized, timeSeconds, speed, ref rand);
            }
            int u = particleSystemNative.flip.x == 1f ? 1 : 0;
            int v = particleSystemNative.flip.y == 1f ? 1 : 0;
            texcoord0[vertexIndex] = Vector2.Scale(uvScale, new Vector2(u, v)) + uvOffset;
            texcoord0[vertexIndex + 1] = Vector2.Scale(uvScale, new Vector2(u, 1 - v)) + uvOffset;
            texcoord0[vertexIndex + 2] = Vector2.Scale(uvScale, new Vector2(1 - u, 1 - v)) + uvOffset;
            texcoord0[vertexIndex + 3] = Vector2.Scale(uvScale, new Vector2(1 - u, v)) + uvOffset;
            var custom1 = particleSystemNative.customData.enabled ? particleSystemNative.customData.GetCustom1(timeNormalized, ref rand) : new Vector4();
            var custom2 = particleSystemNative.customData.enabled ? particleSystemNative.customData.GetCustom2(timeNormalized, ref rand) : new Vector4();
            if (particleSystemNative.customVertexStreams.custom1Active || particleSystemNative.customVertexStreams.custom2Active)
            {
                texcoord0[vertexIndex] = particleSystemNative.customVertexStreams.GetTexcoord0(texcoord0[vertexIndex], ref custom1, ref custom2);
                texcoord0[vertexIndex + 1] = particleSystemNative.customVertexStreams.GetTexcoord0(texcoord0[vertexIndex + 1], ref custom1, ref custom2);
                texcoord0[vertexIndex + 2] = particleSystemNative.customVertexStreams.GetTexcoord0(texcoord0[vertexIndex + 2], ref custom1, ref custom2);
                texcoord0[vertexIndex + 3] = particleSystemNative.customVertexStreams.GetTexcoord0(texcoord0[vertexIndex + 3], ref custom1, ref custom2);
                texcoord1[vertexIndex] = particleSystemNative.customVertexStreams.GetTexcoord1(ref custom1, ref custom2);
                texcoord1[vertexIndex + 1] = texcoord1[vertexIndex];
                texcoord1[vertexIndex + 2] = texcoord1[vertexIndex];
                texcoord1[vertexIndex + 3] = texcoord1[vertexIndex];
            }
            int index = indexOffset + i * 6;
            indices[index] = vertexIndex;
            indices[index + 1] = indices[index] + 1;
            indices[index + 2] = indices[index] + 2;
            indices[index + 3] = indices[index] + 2;
            indices[index + 4] = indices[index] + 3;
            indices[index + 5] = indices[index];
        }
    }
    [BurstCompile]
    struct TransformJob : IJobParallelFor
    {
        public Matrix4x4 matrix;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Vector3> vertices;
        public int vertexBase;
        public void Execute(int i)
        {
            int vertexIndex = vertexBase + i;
            vertices[vertexIndex] = matrix * new Vector4(vertices[vertexIndex].x, vertices[vertexIndex].y, vertices[vertexIndex].z, 1);
        }
    }
    [BurstCompile]
    struct ColorSpaceJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Color> colors;
        public int vertexBase;
        public void Execute(int i)
        {
            int vertexIndex = vertexBase + i;
            float3 sRGB = math.pow(new float3(colors[vertexIndex].r, colors[vertexIndex].g, colors[vertexIndex].b), 1.0f / 2.2f);
            colors[vertexIndex] = new Vector4(sRGB.x, sRGB.y, sRGB.z, colors[vertexIndex].a);
        }
    }
    [BurstCompile]
    struct IndexJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> indices;
        public int indexOffset;
        public int vertexBase;
        public void Execute(int i)
        {
            int index = indexOffset + i;
            indices[index] = vertexBase + indices[index];
        }
    }
}