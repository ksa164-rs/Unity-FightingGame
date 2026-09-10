using System;
using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    /// <summary>
    /// FighterControllerの状態を見た目へ反映する、非ゲームプレイ層です。
    /// Animatorやデバッグ描画の都合が、戦闘フレーム処理へ混ざらないように分離しています。
    /// </summary>
    internal sealed class FighterPresentation : IDisposable
    {
        private readonly Transform owner;
        private readonly Transform visualRoot;
        private readonly Animator animator;
        private readonly Vector3 visualFacingEuler;
        private readonly Vector3 mirroredFacingRotationOffset;
        private readonly Vector3 mirroredScaleMultiplier;
        private readonly bool showAttackHitboxes;
        private readonly bool showHurtboxes;
        private readonly float debugLineWidth;
        private readonly List<RendererTintTarget> rendererTintTargets = new List<RendererTintTarget>();
        private readonly List<LineRenderer> debugAttackLines = new List<LineRenderer>();
        private readonly HashSet<int> floatParameters = new HashSet<int>();
        private readonly HashSet<int> boolParameters = new HashSet<int>();
        private readonly HashSet<int> triggerParameters = new HashSet<int>();

        private Vector3 visualScaleMagnitude = Vector3.one;
        private LineRenderer debugHurtboxLine;
        private Material debugLineMaterial;
        private float animatorSpeedBeforePause = 1f;
        private bool animatorPaused;

        public FighterPresentation(
            Transform owner,
            Transform visualRoot,
            Animator animator,
            Vector3 visualFacingEuler,
            Vector3 mirroredFacingRotationOffset,
            Vector3 mirroredScaleMultiplier,
            bool enablePlayerColorTint,
            Color playerColorTint,
            float playerColorTintStrength,
            bool showAttackHitboxes,
            bool showHurtboxes,
            float debugLineWidth)
        {
            this.owner = owner;
            this.visualRoot = visualRoot;
            this.animator = animator;
            this.visualFacingEuler = visualFacingEuler;
            this.mirroredFacingRotationOffset = mirroredFacingRotationOffset;
            this.mirroredScaleMultiplier = mirroredScaleMultiplier;
            this.showAttackHitboxes = showAttackHitboxes;
            this.showHurtboxes = showHurtboxes;
            this.debugLineWidth = debugLineWidth;

            CacheVisualScale();
            CacheAnimatorParameters();
            ApplyPlayerColorTint(enablePlayerColorTint, playerColorTint, playerColorTintStrength);
        }

        public void SetFacing(bool facingRight)
        {
            if (visualRoot == null)
            {
                return;
            }

            Quaternion baseRotation = Quaternion.Euler(visualFacingEuler);
            Quaternion mirroredRotation = Quaternion.Euler(mirroredFacingRotationOffset);
            visualRoot.localRotation = facingRight ? baseRotation : baseRotation * mirroredRotation;
            visualRoot.localScale = facingRight
                ? visualScaleMagnitude
                : Vector3.Scale(visualScaleMagnitude, mirroredScaleMultiplier);
        }

        public void PlayTrigger(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            int parameterHash = Animator.StringToHash(parameterName);
            if (triggerParameters.Contains(parameterHash))
            {
                animator.SetTrigger(parameterHash);
            }
        }

        public void UpdateAnimator(FighterInputFrame input, FighterState state, bool grounded)
        {
            if (animator == null)
            {
                return;
            }

            SetFloatIfPresent("MoveX", Mathf.Abs(input.moveX));
            SetBoolIfPresent("Crouching", state == FighterState.Crouching);
            SetBoolIfPresent("Grounded", grounded);
        }

        public void SetPaused(bool paused)
        {
            if (animator == null || animatorPaused == paused)
            {
                return;
            }

            if (paused)
            {
                animatorSpeedBeforePause = animator.speed;
                animator.speed = 0f;
            }
            else
            {
                animator.speed = animatorSpeedBeforePause;
            }

            animatorPaused = paused;
        }

        public void UpdateDebug(
            FighterAttackDefinition attack,
            int attackFrame,
            float facingDirection,
            BoxCollider2D hurtbox)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            int visibleAttackLines = 0;
            if (showAttackHitboxes && attack != null)
            {
                Color attackColor = attack.IsThrow
                    ? new Color(1f, 0.2f, 1f, 0.95f)
                    : new Color(1f, 0.12f, 0.08f, 0.95f);

                if (attack.HasFrameHitboxes)
                {
                    IReadOnlyList<AttackHitboxFrame> hitboxes = attack.Hitboxes;
                    for (int i = 0; i < hitboxes.Count; i++)
                    {
                        AttackHitboxFrame hitbox = hitboxes[i];
                        if (hitbox == null || !hitbox.IsActive(attackFrame))
                        {
                            continue;
                        }

                        SetDebugBox(
                            GetOrCreateAttackLine(visibleAttackLines),
                            GetHitboxWorldCenter(hitbox.Center, facingDirection),
                            hitbox.Size,
                            attackColor);
                        visibleAttackLines++;
                    }
                }
                else
                {
                    int activeEnd = attack.StartupFrames + attack.ActiveFrames;
                    if (attackFrame >= attack.StartupFrames && attackFrame < activeEnd)
                    {
                        SetDebugBox(
                            GetOrCreateAttackLine(0),
                            GetHitboxWorldCenter(attack.HitboxCenter, facingDirection),
                            attack.HitboxSize,
                            attackColor);
                        visibleAttackLines = 1;
                    }
                }
            }

            for (int i = visibleAttackLines; i < debugAttackLines.Count; i++)
            {
                debugAttackLines[i].enabled = false;
            }

            if (showHurtboxes && hurtbox != null)
            {
                if (debugHurtboxLine == null)
                {
                    debugHurtboxLine = CreateDebugLine("[Debug] Hurtbox");
                }

                SetDebugBox(
                    debugHurtboxLine,
                    new Vector3(hurtbox.bounds.center.x, hurtbox.bounds.center.y, owner.position.z),
                    new Vector3(hurtbox.bounds.size.x, hurtbox.bounds.size.y, 0f),
                    new Color(0.15f, 1f, 0.25f, 0.9f));
            }
            else if (debugHurtboxLine != null)
            {
                debugHurtboxLine.enabled = false;
            }
        }

        public void DrawSelectedGizmos(FighterAttackDefinition attack, int attackFrame, float facingDirection)
        {
            if (!showAttackHitboxes || attack == null)
            {
                return;
            }

            Gizmos.color = attack.IsThrow
                ? new Color(1f, 0.25f, 1f, 0.6f)
                : new Color(1f, 0.15f, 0.1f, 0.6f);

            if (attack.HasFrameHitboxes)
            {
                IReadOnlyList<AttackHitboxFrame> hitboxes = attack.Hitboxes;
                for (int i = 0; i < hitboxes.Count; i++)
                {
                    AttackHitboxFrame hitbox = hitboxes[i];
                    if (hitbox != null && hitbox.IsActive(attackFrame))
                    {
                        Gizmos.DrawWireCube(
                            GetHitboxWorldCenter(hitbox.Center, facingDirection),
                            hitbox.Size);
                    }
                }
            }
            else
            {
                Gizmos.DrawWireCube(
                    GetHitboxWorldCenter(attack.HitboxCenter, facingDirection),
                    attack.HitboxSize);
            }
        }

        public void Dispose()
        {
            SetPaused(false);
            RestorePlayerColorTint();
            if (debugLineMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(debugLineMaterial);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(debugLineMaterial);
            }
        }

        private void ApplyPlayerColorTint(bool enabled, Color tintColor, float strength)
        {
            if (!enabled || visualRoot == null || strength <= 0f)
            {
                return;
            }

            Color multiplier = Color.Lerp(Color.white, tintColor, Mathf.Clamp01(strength));
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    int colorPropertyId;
                    if (material.HasProperty("_BaseColor"))
                    {
                        colorPropertyId = Shader.PropertyToID("_BaseColor");
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        colorPropertyId = Shader.PropertyToID("_Color");
                    }
                    else
                    {
                        continue;
                    }

                    MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(originalBlock, materialIndex);
                    rendererTintTargets.Add(new RendererTintTarget(renderer, materialIndex, originalBlock));

                    MaterialPropertyBlock tintedBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(tintedBlock, materialIndex);
                    Color baseColor = material.GetColor(colorPropertyId);
                    Color multipliedColor = new Color(
                        baseColor.r * multiplier.r,
                        baseColor.g * multiplier.g,
                        baseColor.b * multiplier.b,
                        baseColor.a);
                    tintedBlock.SetColor(colorPropertyId, multipliedColor);
                    renderer.SetPropertyBlock(tintedBlock, materialIndex);
                }
            }
        }

        private void RestorePlayerColorTint()
        {
            for (int i = 0; i < rendererTintTargets.Count; i++)
            {
                RendererTintTarget target = rendererTintTargets[i];
                if (target.Renderer != null)
                {
                    target.Renderer.SetPropertyBlock(target.OriginalBlock, target.MaterialIndex);
                }
            }

            rendererTintTargets.Clear();
        }

        private sealed class RendererTintTarget
        {
            public RendererTintTarget(
                Renderer renderer,
                int materialIndex,
                MaterialPropertyBlock originalBlock)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                OriginalBlock = originalBlock;
            }

            public Renderer Renderer { get; }
            public int MaterialIndex { get; }
            public MaterialPropertyBlock OriginalBlock { get; }
        }

        private void CacheVisualScale()
        {
            if (visualRoot == null)
            {
                return;
            }

            Vector3 currentScale = visualRoot.localScale;
            visualScaleMagnitude = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(currentScale.x)),
                Mathf.Max(0.0001f, Mathf.Abs(currentScale.y)),
                Mathf.Max(0.0001f, Mathf.Abs(currentScale.z)));
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                int hash = parameters[i].nameHash;
                switch (parameters[i].type)
                {
                    case AnimatorControllerParameterType.Float:
                        floatParameters.Add(hash);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        boolParameters.Add(hash);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        triggerParameters.Add(hash);
                        break;
                }
            }
        }

        private void SetFloatIfPresent(string parameterName, float value)
        {
            int parameterHash = Animator.StringToHash(parameterName);
            if (floatParameters.Contains(parameterHash))
            {
                animator.SetFloat(parameterHash, value);
            }
        }

        private void SetBoolIfPresent(string parameterName, bool value)
        {
            int parameterHash = Animator.StringToHash(parameterName);
            if (boolParameters.Contains(parameterHash))
            {
                animator.SetBool(parameterHash, value);
            }
        }

        private LineRenderer GetOrCreateAttackLine(int index)
        {
            while (debugAttackLines.Count <= index)
            {
                debugAttackLines.Add(CreateDebugLine($"[Debug] Hitbox {debugAttackLines.Count + 1}"));
            }

            return debugAttackLines[index];
        }

        private LineRenderer CreateDebugLine(string objectName)
        {
            if (debugLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ??
                                Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    debugLineMaterial = new Material(shader)
                    {
                        name = "GASG Hitbox Debug Material",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            GameObject lineObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineObject.transform.SetParent(owner, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 16;
            line.widthMultiplier = debugLineWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (debugLineMaterial != null)
            {
                line.sharedMaterial = debugLineMaterial;
            }

            return line;
        }

        private static void SetDebugBox(LineRenderer line, Vector3 center, Vector3 size, Color color)
        {
            Vector3 half = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f) * 0.5f;
            Vector3 p0 = center + new Vector3(-half.x, -half.y, 0f);
            Vector3 p1 = center + new Vector3(half.x, -half.y, 0f);
            Vector3 p2 = center + new Vector3(half.x, half.y, 0f);
            Vector3 p3 = center + new Vector3(-half.x, half.y, 0f);

            line.enabled = true;
            line.positionCount = 9;
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
            line.SetPosition(2, p2);
            line.SetPosition(3, p3);
            line.SetPosition(4, p0);
            line.SetPosition(5, p1);
            line.SetPosition(6, p2);
            line.SetPosition(7, p3);
            line.SetPosition(8, p0);
        }

        private Vector3 GetHitboxWorldCenter(Vector3 localCenter, float facingDirection)
        {
            return owner.position + new Vector3(
                localCenter.x * facingDirection,
                localCenter.y,
                localCenter.z);
        }
    }
}
