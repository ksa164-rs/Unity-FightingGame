using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    /// <summary>
    /// 既存の待機モーションを基準に、Genericリグ向けの基本移動モーションを非破壊で生成します。
    /// Unity 6000.3.11f1で作成・検証しています。
    /// </summary>
    public sealed class BasicLocomotionClipGenerator : EditorWindow
    {
        private const string BaseClipPath = "Assets/GASGFighter/Graphics/3D/Chara/animations/AS_p01_000.anim";
        private const string CharacterPrefabPath = "Assets/GASGFighter/Graphics/3D/Chara/prefabs/Player_001.prefab";
        private const string OutputDirectory = "Assets/GASGFighter/Graphics/3D/Chara/animations/Locomotion";

        private const string WalkPath = OutputDirectory + "/AS_p01_004.anim";
        private const string ForwardStepPath = OutputDirectory + "/AS_p01_005.anim";
        private const string BackwardStepPath = OutputDirectory + "/AS_p01_006.anim";

        private const string HipsPath = "root/hips";
        private const string SpinePath = "root/hips/spineA";
        private const string LeftThighPath = "root/hips/tg_L";
        private const string LeftShinPath = "root/hips/tg_L/leg_L";
        private const string LeftFootPath = "root/hips/tg_L/leg_L/foot_L";
        private const string LeftToePath = "root/hips/tg_L/leg_L/foot_L/toe_L";
        private const string RightThighPath = "root/hips/tg_R";
        private const string RightShinPath = "root/hips/tg_R/leg_R";
        private const string RightFootPath = "root/hips/tg_R/leg_R/foot_R";
        private const string RightToePath = "root/hips/tg_R/leg_R/foot_R/toe_R";
        private const string LeftArmPath = "root/hips/spineA/spineB/spineC/spineD/shol_L/arm_L";
        private const string LeftForearmPath = LeftArmPath + "/foreArm_L";
        private const string RightArmPath = "root/hips/spineA/spineB/spineC/spineD/shol_R/arm_R";
        private const string RightForearmPath = RightArmPath + "/foreArm_R";

        [SerializeField] private float walkDuration = 1.0f;
        [SerializeField] private float legSwingDegrees = 24.0f;
        [SerializeField] private float kneeBendDegrees = 34.0f;
        [SerializeField] private float armSwingDegrees = 17.0f;
        [SerializeField] private float hipBobMeters = 0.015f;
        [SerializeField] private bool createStepMotions = true;
        [SerializeField] private bool overwriteExisting;

        private enum MotionKind
        {
            WalkLoop,
            ForwardStep,
            BackwardStep
        }

        private sealed class RigContext
        {
            public GameObject Instance;
            public Vector3 Up;
            public Vector3 Forward;
            public Vector3 Lateral;
            public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
            public readonly Dictionary<string, float> ForwardRotationSigns = new Dictionary<string, float>();
            public readonly Dictionary<string, float> BendRotationSigns = new Dictionary<string, float>();
        }

        [MenuItem("GASG/対戦プロトタイプ/02. モーションと技/基本移動モーションを作成")]
        private static void OpenWindow()
        {
            GetWindow<BasicLocomotionClipGenerator>("基本移動モーション");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("基本移動モーション生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "wait.animを基準に、既存データを変更せず新規Animation Clipを生成します。歩行はインプレースのLOOP、ステップはワンショットです。",
                MessageType.Info);

            walkDuration = EditorGUILayout.Slider("歩行1周（秒）", walkDuration, 0.65f, 1.35f);
            legSwingDegrees = EditorGUILayout.Slider("脚の振り幅", legSwingDegrees, 10.0f, 36.0f);
            kneeBendDegrees = EditorGUILayout.Slider("膝の曲げ", kneeBendDegrees, 15.0f, 50.0f);
            armSwingDegrees = EditorGUILayout.Slider("腕の振り幅", armSwingDegrees, 5.0f, 28.0f);
            hipBobMeters = EditorGUILayout.Slider("腰の上下動（m）", hipBobMeters, 0.0f, 0.035f);
            createStepMotions = EditorGUILayout.Toggle("前後ステップも生成", createStepMotions);
            overwriteExisting = EditorGUILayout.Toggle("既存の生成物を上書き", overwriteExisting);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("生成", GUILayout.Height(32.0f)))
                    GenerateAll(overwriteExisting, createStepMotions, walkDuration, legSwingDegrees, kneeBendDegrees, armSwingDegrees, hipBobMeters);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("出力先", OutputDirectory);
        }

        /// <summary>バッチモード用。既存ファイルがある場合は安全のため失敗します。</summary>
        public static void GenerateDefaultAssets()
        {
            try
            {
                GenerateAll(false, true, 1.0f, 24.0f, 34.0f, 17.0f, 0.015f);
                Debug.Log("[BasicLocomotion] SUCCESS: 基本移動モーションの生成が完了しました。");
            }
            catch (Exception exception)
            {
                Debug.LogError("[BasicLocomotion] FAILED: " + exception);
                throw;
            }
        }

        /// <summary>検証用コピー内の生成物だけを更新します。</summary>
        public static void RegenerateDefaultAssetsForValidation()
        {
            try
            {
                GenerateAll(true, true, 1.0f, 24.0f, 34.0f, 17.0f, 0.015f);
                Debug.Log("[BasicLocomotion] SUCCESS: 検証用モーションを再生成しました。");
            }
            catch (Exception exception)
            {
                Debug.LogError("[BasicLocomotion] FAILED: " + exception);
                throw;
            }
        }

        /// <summary>生成済みClipの尺、Loop設定、歩行の継ぎ目を検証します。</summary>
        public static void ValidateDefaultAssets()
        {
            ValidateClip(WalkPath, 1.0f, true);
            ValidateClip(ForwardStepPath, 0.46f, false);
            ValidateClip(BackwardStepPath, 0.50f, false);

            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (walk == null || prefab == null)
                throw new InvalidOperationException("歩行Clipまたは確認用Prefabが見つかりません。");

            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                walk.SampleAnimation(instance, 0.0f);
                Transform hips = instance.transform.Find(HipsPath);
                Transform leftFoot = instance.transform.Find(LeftFootPath);
                Transform rightFoot = instance.transform.Find(RightFootPath);
                if (hips == null || leftFoot == null || rightFoot == null)
                    throw new InvalidOperationException("Loop検証に必要なボーンが見つかりません。");

                Vector3 hipsStart = hips.localPosition;
                Quaternion hipsRotationStart = hips.localRotation;
                Vector3 leftFootStart = leftFoot.localPosition;
                Quaternion leftFootRotationStart = leftFoot.localRotation;
                Vector3 rightFootStart = rightFoot.localPosition;
                Quaternion rightFootRotationStart = rightFoot.localRotation;

                walk.SampleAnimation(instance, walk.length);
                RequireNear(hipsStart, hips.localPosition, 0.0001f, "腰位置のLoop継ぎ目");
                RequireNear(hipsRotationStart, hips.localRotation, 0.05f, "腰回転のLoop継ぎ目");
                RequireNear(leftFootStart, leftFoot.localPosition, 0.0001f, "左足位置のLoop継ぎ目");
                RequireNear(leftFootRotationStart, leftFoot.localRotation, 0.05f, "左足回転のLoop継ぎ目");
                RequireNear(rightFootStart, rightFoot.localPosition, 0.0001f, "右足位置のLoop継ぎ目");
                RequireNear(rightFootRotationStart, rightFoot.localRotation, 0.05f, "右足回転のLoop継ぎ目");
            }
            finally
            {
                if (instance != null)
                    DestroyImmediate(instance);
            }

            Debug.Log("[BasicLocomotion] VALIDATION SUCCESS: 尺、Loop設定、歩行の継ぎ目を確認しました。");
        }

        private static void ValidateClip(string path, float expectedLength, bool expectedLoop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                throw new FileNotFoundException("検証対象Clipが見つかりません。", path);
            if (Mathf.Abs(clip.length - expectedLength) > 1.0f / 30.0f)
                throw new InvalidOperationException(string.Format(
                    "Clip尺が想定外です: {0}, expected={1:F3}, actual={2:F3}", path, expectedLength, clip.length));

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime != expectedLoop)
                throw new InvalidOperationException("Loop設定が想定外です: " + path);

            Debug.Log(string.Format(
                "[BasicLocomotion] VALID: {0}, length={1:F3}s, fps={2:F0}, loop={3}",
                path,
                clip.length,
                clip.frameRate,
                settings.loopTime));
        }

        private static void RequireNear(Vector3 expected, Vector3 actual, float tolerance, string label)
        {
            if (Vector3.Distance(expected, actual) > tolerance)
                throw new InvalidOperationException(label + "が一致しません。");
        }

        private static void RequireNear(Quaternion expected, Quaternion actual, float toleranceDegrees, string label)
        {
            if (Quaternion.Angle(expected, actual) > toleranceDegrees)
                throw new InvalidOperationException(label + "が一致しません。");
        }

        private static void GenerateAll(
            bool overwrite,
            bool includeSteps,
            float duration,
            float legSwing,
            float kneeBend,
            float armSwing,
            float hipBob)
        {
            AnimationClip baseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BaseClipPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (baseClip == null)
                throw new FileNotFoundException("基準Animation Clipが見つかりません。", BaseClipPath);
            if (prefab == null)
                throw new FileNotFoundException("キャラクターPrefabが見つかりません。", CharacterPrefabPath);

            EnsureOutputDirectory();

            var requestedPaths = new List<string> { WalkPath };
            if (includeSteps)
            {
                requestedPaths.Add(ForwardStepPath);
                requestedPaths.Add(BackwardStepPath);
            }

            if (!overwrite)
            {
                foreach (string path in requestedPaths)
                {
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                        throw new IOException("既存ファイルを保護するため生成を中止しました: " + path);
                }
            }

            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                RigContext rig = BuildRigContext(instance, baseClip);

                CreateClip(baseClip, rig, MotionKind.WalkLoop, WalkPath, "AS_p01_004", duration,
                    legSwing, kneeBend, armSwing, hipBob, overwrite);

                if (includeSteps)
                {
                    CreateClip(baseClip, rig, MotionKind.ForwardStep, ForwardStepPath, "AS_p01_005", 0.46f,
                        31.0f, 43.0f, 13.0f, 0.018f, overwrite);
                    CreateClip(baseClip, rig, MotionKind.BackwardStep, BackwardStepPath, "AS_p01_006", 0.50f,
                        27.0f, 39.0f, 12.0f, 0.016f, overwrite);
                }
            }
            finally
            {
                if (instance != null)
                    DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(string.Format(
                "[BasicLocomotion] SUCCESS: {0}個のClipを生成しました。出力先: {1}",
                requestedPaths.Count,
                OutputDirectory));
        }

        private static RigContext BuildRigContext(GameObject instance, AnimationClip baseClip)
        {
            baseClip.SampleAnimation(instance, 0.0f);
            var rig = new RigContext { Instance = instance };

            string[] paths =
            {
                HipsPath, SpinePath,
                LeftThighPath, LeftShinPath, LeftFootPath, LeftToePath,
                RightThighPath, RightShinPath, RightFootPath, RightToePath,
                LeftArmPath, LeftForearmPath, RightArmPath, RightForearmPath
            };

            foreach (string path in paths)
            {
                Transform transform = instance.transform.Find(path);
                if (transform == null)
                    throw new InvalidOperationException("必須ボーンが見つかりません: " + path);
                rig.Bones.Add(path, transform);
            }

            Vector3 hipsPosition = rig.Bones[HipsPath].position;
            Vector3 spinePosition = rig.Bones[SpinePath].position;
            rig.Up = (spinePosition - hipsPosition).normalized;

            Vector3 leftToeDirection = rig.Bones[LeftToePath].position - rig.Bones[LeftFootPath].position;
            Vector3 rightToeDirection = rig.Bones[RightToePath].position - rig.Bones[RightFootPath].position;
            Vector3 forward = Vector3.ProjectOnPlane(leftToeDirection + rightToeDirection, rig.Up).normalized;
            if (forward.sqrMagnitude < 0.5f)
                throw new InvalidOperationException("足先からキャラクターの前方向を判定できませんでした。");
            rig.Forward = forward;

            Vector3 lateral = Vector3.Cross(rig.Up, rig.Forward).normalized;
            Vector3 leftToRight = rig.Bones[RightThighPath].position - rig.Bones[LeftThighPath].position;
            if (Vector3.Dot(lateral, leftToRight) < 0.0f)
                lateral = -lateral;
            rig.Lateral = lateral;

            RegisterForwardSign(rig, LeftThighPath, LeftShinPath);
            RegisterForwardSign(rig, RightThighPath, RightShinPath);
            RegisterForwardSign(rig, LeftFootPath, LeftToePath);
            RegisterForwardSign(rig, RightFootPath, RightToePath);
            RegisterForwardSign(rig, LeftArmPath, LeftForearmPath);
            RegisterForwardSign(rig, RightArmPath, RightForearmPath);
            RegisterBendSign(rig, LeftShinPath, LeftFootPath);
            RegisterBendSign(rig, RightShinPath, RightFootPath);

            return rig;
        }

        private static void RegisterForwardSign(RigContext rig, string bonePath, string childPath)
        {
            Vector3 direction = (rig.Bones[childPath].position - rig.Bones[bonePath].position).normalized;
            float plus = Vector3.Dot(Quaternion.AngleAxis(10.0f, rig.Lateral) * direction, rig.Forward);
            float minus = Vector3.Dot(Quaternion.AngleAxis(-10.0f, rig.Lateral) * direction, rig.Forward);
            rig.ForwardRotationSigns[bonePath] = plus >= minus ? 1.0f : -1.0f;
        }

        private static void RegisterBendSign(RigContext rig, string bonePath, string childPath)
        {
            Vector3 direction = (rig.Bones[childPath].position - rig.Bones[bonePath].position).normalized;
            float plus = Vector3.Dot(Quaternion.AngleAxis(10.0f, rig.Lateral) * direction, rig.Forward);
            float minus = Vector3.Dot(Quaternion.AngleAxis(-10.0f, rig.Lateral) * direction, rig.Forward);
            rig.BendRotationSigns[bonePath] = plus <= minus ? 1.0f : -1.0f;
        }

        private static void CreateClip(
            AnimationClip baseClip,
            RigContext rig,
            MotionKind kind,
            string outputPath,
            string clipName,
            float duration,
            float legSwing,
            float kneeBend,
            float armSwing,
            float hipBob,
            bool overwrite)
        {
            AnimationClip clip = new AnimationClip();
            EditorUtility.CopySerialized(baseClip, clip);
            clip.name = clipName;
            clip.frameRate = 30.0f;
            ScaleBaseCurves(clip, baseClip, duration);

            var rotations = new Dictionary<string, Quaternion[]>();
            string[] rotationPaths =
            {
                HipsPath, SpinePath,
                LeftThighPath, LeftShinPath, LeftFootPath,
                RightThighPath, RightShinPath, RightFootPath,
                LeftArmPath, RightArmPath
            };
            foreach (string path in rotationPaths)
                rotations[path] = new Quaternion[GetFrameCount(duration) + 1];

            int frameCount = GetFrameCount(duration);
            var hipsPositions = new Vector3[frameCount + 1];

            for (int frame = 0; frame <= frameCount; frame++)
            {
                float normalizedTime = frame / (float)frameCount;
                float time = normalizedTime * duration;
                float baseTime = Mathf.Repeat(normalizedTime, 1.0f) * baseClip.length;
                if (frame == frameCount)
                    baseTime = 0.0f;
                baseClip.SampleAnimation(rig.Instance, baseTime);

                MotionPose pose = EvaluatePose(kind, normalizedTime, legSwing, kneeBend, armSwing, hipBob);
                rotations[HipsPath][frame] = RotateAroundWorldAxis(rig.Bones[HipsPath], rig.Up, pose.HipTwist);
                rotations[SpinePath][frame] = RotateAroundWorldAxis(rig.Bones[SpinePath], rig.Up, -pose.HipTwist * 0.55f);

                rotations[LeftThighPath][frame] = RotateForward(rig, LeftThighPath, pose.LeftLeg);
                rotations[RightThighPath][frame] = RotateForward(rig, RightThighPath, pose.RightLeg);
                rotations[LeftShinPath][frame] = RotateBend(rig, LeftShinPath, pose.LeftKnee);
                rotations[RightShinPath][frame] = RotateBend(rig, RightShinPath, pose.RightKnee);
                rotations[LeftFootPath][frame] = RotateForward(rig, LeftFootPath, -pose.LeftLeg * 0.42f);
                rotations[RightFootPath][frame] = RotateForward(rig, RightFootPath, -pose.RightLeg * 0.42f);
                rotations[LeftArmPath][frame] = RotateForward(rig, LeftArmPath, pose.LeftArm);
                rotations[RightArmPath][frame] = RotateForward(rig, RightArmPath, pose.RightArm);

                Transform hips = rig.Bones[HipsPath];
                Vector3 localUp = hips.parent != null ? hips.parent.InverseTransformDirection(rig.Up) : rig.Up;
                Vector3 localSide = hips.parent != null ? hips.parent.InverseTransformDirection(rig.Lateral) : rig.Lateral;
                hipsPositions[frame] = hips.localPosition + localUp * pose.HipHeight + localSide * pose.HipSide;
            }

            // LOOPおよび待機姿勢へ戻るワンショットの終端を、開始値と完全一致させる。
            foreach (string path in rotationPaths)
                rotations[path][frameCount] = rotations[path][0];
            hipsPositions[frameCount] = hipsPositions[0];

            foreach (KeyValuePair<string, Quaternion[]> pair in rotations)
                SetQuaternionCurves(clip, pair.Key, pair.Value, duration);
            SetPositionCurves(clip, HipsPath, hipsPositions, duration);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = kind == MotionKind.WalkLoop;
            settings.loopBlend = kind == MotionKind.WalkLoop;
            settings.loopBlendOrientation = kind == MotionKind.WalkLoop;
            settings.loopBlendPositionY = kind == MotionKind.WalkLoop;
            settings.loopBlendPositionXZ = kind == MotionKind.WalkLoop;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (overwrite)
            {
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    existing.name = clipName;
                    EditorUtility.SetDirty(existing);
                    DestroyImmediate(clip);
                    Debug.Log("[BasicLocomotion] UPDATED: " + outputPath);
                    return;
                }
            }

            AssetDatabase.CreateAsset(clip, outputPath);
            Debug.Log("[BasicLocomotion] CREATED: " + outputPath);
        }

        private struct MotionPose
        {
            public float LeftLeg;
            public float RightLeg;
            public float LeftKnee;
            public float RightKnee;
            public float LeftArm;
            public float RightArm;
            public float HipHeight;
            public float HipSide;
            public float HipTwist;
        }

        private static MotionPose EvaluatePose(
            MotionKind kind,
            float t,
            float legSwing,
            float kneeBend,
            float armSwing,
            float hipBob)
        {
            if (kind == MotionKind.WalkLoop)
            {
                float cycle = Mathf.Cos(t * Mathf.PI * 2.0f);
                float leftSwing = cycle * legSwing;
                float rightSwing = -leftSwing;
                float leftLift = Mathf.Pow(Mathf.Max(0.0f, -Mathf.Sin(t * Mathf.PI * 2.0f)), 1.35f);
                float rightLift = Mathf.Pow(Mathf.Max(0.0f, Mathf.Sin(t * Mathf.PI * 2.0f)), 1.35f);
                return new MotionPose
                {
                    LeftLeg = leftSwing,
                    RightLeg = rightSwing,
                    LeftKnee = 5.0f + leftLift * kneeBend,
                    RightKnee = 5.0f + rightLift * kneeBend,
                    LeftArm = -cycle * armSwing,
                    RightArm = cycle * armSwing,
                    HipHeight = -Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2.0f)) * hipBob,
                    HipSide = Mathf.Sin(t * Mathf.PI * 2.0f) * 0.007f,
                    HipTwist = Mathf.Sin(t * Mathf.PI * 2.0f) * 4.0f
                };
            }

            float envelope = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            float stride = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 2.0f);
            float direction = kind == MotionKind.ForwardStep ? 1.0f : -1.0f;
            float leadLeg = direction * legSwing * envelope;
            float trailLeg = -direction * legSwing * 0.72f * envelope;
            float recovery = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI), 1.4f);
            return new MotionPose
            {
                LeftLeg = leadLeg + stride * 3.0f,
                RightLeg = trailLeg - stride * 3.0f,
                LeftKnee = 7.0f + kneeBend * recovery * (kind == MotionKind.ForwardStep ? 0.55f : 0.95f),
                RightKnee = 7.0f + kneeBend * recovery * (kind == MotionKind.ForwardStep ? 0.90f : 0.60f),
                LeftArm = -leadLeg / Mathf.Max(1.0f, legSwing) * armSwing,
                RightArm = -trailLeg / Mathf.Max(1.0f, legSwing) * armSwing,
                HipHeight = -hipBob * recovery,
                HipSide = Mathf.Sin(t * Mathf.PI) * 0.006f,
                HipTwist = -direction * envelope * 4.5f
            };
        }

        private static Quaternion RotateForward(RigContext rig, string path, float degrees)
        {
            float sign;
            if (!rig.ForwardRotationSigns.TryGetValue(path, out sign))
                sign = 1.0f;
            return RotateAroundWorldAxis(rig.Bones[path], rig.Lateral, degrees * sign);
        }

        private static Quaternion RotateBend(RigContext rig, string path, float degrees)
        {
            return RotateAroundWorldAxis(rig.Bones[path], rig.Lateral, degrees * rig.BendRotationSigns[path]);
        }

        private static Quaternion RotateAroundWorldAxis(Transform bone, Vector3 worldAxis, float degrees)
        {
            Quaternion desiredWorld = Quaternion.AngleAxis(degrees, worldAxis) * bone.rotation;
            return bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * desiredWorld : desiredWorld;
        }

        private static int GetFrameCount(float duration)
        {
            return Mathf.Max(2, Mathf.RoundToInt(duration * 30.0f));
        }

        private static void ScaleBaseCurves(AnimationClip destination, AnimationClip source, float duration)
        {
            float timeScale = duration / Mathf.Max(0.0001f, source.length);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                if (sourceCurve == null)
                    continue;

                Keyframe[] sourceKeys = sourceCurve.keys;
                var scaledKeys = new Keyframe[sourceKeys.Length];
                for (int index = 0; index < sourceKeys.Length; index++)
                {
                    Keyframe key = sourceKeys[index];
                    scaledKeys[index] = new Keyframe(
                        key.time * timeScale,
                        key.value,
                        key.inTangent / timeScale,
                        key.outTangent / timeScale,
                        key.inWeight,
                        key.outWeight)
                    {
                        weightedMode = key.weightedMode
                    };
                }
                var scaled = new AnimationCurve(scaledKeys)
                {
                    preWrapMode = sourceCurve.preWrapMode,
                    postWrapMode = sourceCurve.postWrapMode
                };
                AnimationUtility.SetEditorCurve(destination, binding, scaled);
            }
        }

        private static void SetQuaternionCurves(AnimationClip clip, string path, Quaternion[] values, float duration)
        {
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            var w = new AnimationCurve();
            Quaternion previous = values[0];
            for (int i = 0; i < values.Length; i++)
            {
                Quaternion value = values[i];
                if (i > 0 && Quaternion.Dot(previous, value) < 0.0f)
                    value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
                previous = value;
                float time = duration * i / (values.Length - 1.0f);
                x.AddKey(time, value.x);
                y.AddKey(time, value.y);
                z.AddKey(time, value.z);
                w.AddKey(time, value.w);
            }

            SetAutoTangents(x);
            SetAutoTangents(y);
            SetAutoTangents(z);
            SetAutoTangents(w);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), x);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), y);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), z);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), w);
        }

        private static void SetPositionCurves(AnimationClip clip, string path, Vector3[] values, float duration)
        {
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            for (int i = 0; i < values.Length; i++)
            {
                float time = duration * i / (values.Length - 1.0f);
                x.AddKey(time, values[i].x);
                y.AddKey(time, values[i].y);
                z.AddKey(time, values[i].z);
            }
            SetAutoTangents(x);
            SetAutoTangents(y);
            SetAutoTangents(z);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), x);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), y);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), z);
        }

        private static void SetAutoTangents(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
        }

        private static void EnsureOutputDirectory()
        {
            if (AssetDatabase.IsValidFolder(OutputDirectory))
                return;

            string current = "Assets";
            string[] segments = OutputDirectory.Substring("Assets/".Length).Split('/');
            foreach (string segment in segments)
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
