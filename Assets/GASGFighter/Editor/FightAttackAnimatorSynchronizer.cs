using System.Collections.Generic;
using GASG.Fighting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public static class FightAttackAnimatorSynchronizer
    {
        private const string ControllerPath = "Assets/GASGFighter/Graphics/3D/Chara/animations/Player_001.controller";
        private const string AnimationProfilePath = "Assets/GASGFighter/Data/AnimationProfile_Prototype.asset";

        public static bool Sync(FighterAttackDatabase database)
        {
            if (database == null)
            {
                return false;
            }

            // 同じStateへ異なる尺やClipを書き込むと最後の技だけが残るため、変更前に中止する。
            List<FighterAttackDefinition> attacks = CollectUniqueAttacks(database);
            var actionsById = new Dictionary<string, FighterAttackDefinition>(System.StringComparer.Ordinal);
            for (int i = 0; i < attacks.Count; i++)
            {
                FighterAttackDefinition attack = attacks[i];
                if (attack == null || string.IsNullOrWhiteSpace(attack.ActionId)) continue;
                if (actionsById.TryGetValue(attack.ActionId, out FighterAttackDefinition other) &&
                    (attack.TotalFrames != other.TotalFrames || attack.AnimationClip != other.AnimationClip))
                {
                    Debug.LogWarning($"[GASG Fighter][スキップ] Animator同期を中止しました。{other.DisplayName} と {attack.DisplayName} は同じAction ID ({attack.ActionId}) ですが尺またはClipが異なります。Actionを分けてから再実行してください。Controllerは変更していません。", database);
                    return false;
                }
                actionsById[attack.ActionId] = attack;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.layers.Length == 0)
            {
                Debug.LogWarning($"[GASG Fighter] Animator Controllerを同期できません: {ControllerPath}");
                return false;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindState(stateMachine, "Idle");
            if (idleState == null)
            {
                idleState = stateMachine.AddState("Idle", new Vector3(180f, 80f, 0f));
                idleState.writeDefaultValues = true;
                stateMachine.defaultState = idleState;
            }

            FighterAnimationProfile animationProfile =
                AssetDatabase.LoadAssetAtPath<FighterAnimationProfile>(AnimationProfilePath);
            SyncBaseActions(controller, stateMachine, idleState, animationProfile);

            int stateIndex = 0;
            for (int attackIndex = 0; attackIndex < attacks.Count; attackIndex++)
            {
                FighterAttackDefinition attack = attacks[attackIndex];
                if (attack == null || attack.AnimationClip == null || string.IsNullOrWhiteSpace(attack.ActionId))
                {
                    continue;
                }

                if (!EnsureTrigger(controller, attack.ActionId))
                {
                    continue;
                }

                AnimatorState state = FindState(stateMachine, attack.ActionId);
                if (state == null)
                {
                    // Clipが同じでもActionが違えば別State。別技のStateを改名・転用しない。
                    state = stateMachine.AddState(
                        attack.ActionId,
                        new Vector3(430f, 40f + stateIndex * 70f, 0f));
                }

                state.motion = attack.AnimationClip;
                state.writeDefaultValues = true;
                // Clip尺をゲームプレイ上の総フレームへ合わせる。判定側は常に60Hz整数で管理する。
                float gameplayDuration = attack.TotalFrames / (float)FightFrameTiming.SimulationRate;
                state.speed = gameplayDuration > 0f
                    ? Mathf.Max(0.01f, attack.AnimationClip.length / gameplayDuration)
                    : 1f;
                EnsureAnyStateTransition(stateMachine, state, attack.ActionId);
                EnsureReturnTransition(state, idleState);
                EditorUtility.SetDirty(state);
                stateIndex++;
            }

            EditorUtility.SetDirty(idleState);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static void SyncBaseActions(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            AnimatorState idleState,
            FighterAnimationProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (profile.Idle != null && profile.Idle.AnimationClip != null)
            {
                idleState.motion = profile.Idle.AnimationClip;
                idleState.writeDefaultValues = true;
            }

            if (profile.CrouchIdle != null && profile.CrouchIdle.AnimationClip != null)
            {
                EnsureBool(controller, "Crouching");
                AnimatorState crouchState = FindState(stateMachine, "CrouchIdle");
                if (crouchState == null)
                {
                    crouchState = stateMachine.AddState("CrouchIdle", new Vector3(430f, 120f, 0f));
                }

                crouchState.motion = profile.CrouchIdle.AnimationClip;
                crouchState.speed = 1f;
                crouchState.writeDefaultValues = true;
                EnsureBoolTransition(idleState, crouchState, "Crouching", true);
                EnsureBoolTransition(crouchState, idleState, "Crouching", false);
                EditorUtility.SetDirty(crouchState);
            }

            int triggerIndex = 0;
            foreach (FighterActionDefinition action in profile.TriggerActions)
            {
                if (action == null || action.AnimationClip == null || string.IsNullOrWhiteSpace(action.ActionId))
                {
                    continue;
                }

                if (!EnsureTrigger(controller, action.ActionId))
                {
                    continue;
                }

                AnimatorState state = FindState(stateMachine, action.ActionId);
                if (state == null)
                {
                    state = stateMachine.AddState(action.ActionId, new Vector3(680f, 40f + triggerIndex * 65f, 0f));
                }

                state.name = action.ActionId;
                state.motion = action.AnimationClip;
                state.speed = 1f;
                state.writeDefaultValues = true;
                EnsureAnyStateTransition(stateMachine, state, action.ActionId);
                EnsureReturnTransition(state, idleState);
                EditorUtility.SetDirty(state);
                triggerIndex++;
            }
        }

        private static List<FighterAttackDefinition> CollectUniqueAttacks(FighterAttackDatabase database)
        {
            List<FighterAttackDefinition> attacks = new List<FighterAttackDefinition>();
            HashSet<FighterAttackDefinition> registered = new HashSet<FighterAttackDefinition>();

            foreach (AttackMotionId motionId in System.Enum.GetValues(typeof(AttackMotionId)))
            {
                AddAttackIfUnique(database.GetAttack(motionId), registered, attacks);
            }

            IReadOnlyList<FighterMoveBinding> bindings = database.MoveBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                FighterMoveBinding binding = bindings[i];
                if (binding != null)
                {
                    AddAttackIfUnique(binding.Attack, registered, attacks);
                }
            }

            return attacks;
        }

        private static void AddAttackIfUnique(
            FighterAttackDefinition attack,
            HashSet<FighterAttackDefinition> registered,
            List<FighterAttackDefinition> attacks)
        {
            if (attack != null && registered.Add(attack))
            {
                attacks.Add(attack);
            }
        }

        private static bool EnsureTrigger(AnimatorController controller, string triggerName)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != triggerName)
                {
                    continue;
                }

                if (parameters[i].type == AnimatorControllerParameterType.Trigger)
                {
                    return true;
                }

                Debug.LogError($"[GASG Fighter] Animator Parameter '{triggerName}' はTrigger以外の型で登録されています。");
                return false;
            }

            controller.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
            return true;
        }

        private static bool EnsureBool(AnimatorController controller, string parameterName)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != parameterName)
                {
                    continue;
                }

                if (parameters[i].type == AnimatorControllerParameterType.Bool)
                {
                    return true;
                }

                Debug.LogError($"[GASG Fighter] Animator Parameter '{parameterName}' はBool以外の型で登録されています。");
                return false;
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
            return true;
        }

        private static void EnsureBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameterName,
            bool expectedValue)
        {
            AnimatorStateTransition transition = null;
            AnimatorStateTransition[] transitions = source.transitions;
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].destinationState == destination)
                {
                    transition = transitions[i];
                    break;
                }
            }

            if (transition == null)
            {
                transition = source.AddTransition(destination);
            }

            AnimatorCondition[] existingConditions = transition.conditions;
            for (int i = existingConditions.Length - 1; i >= 0; i--)
            {
                transition.RemoveCondition(existingConditions[i]);
            }

            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameterName);
            EditorUtility.SetDirty(transition);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == stateName)
                {
                    return states[i].state;
                }
            }

            return null;
        }

        private static void EnsureAnyStateTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string triggerName)
        {
            // 被弾中の追撃でも怯みを先頭から再生し、同期後もこの設定を保持する。
            bool canRestartOnHit = triggerName == "LightHit";
            AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
            for (int i = 0; i < transitions.Length; i++)
            {
                AnimatorStateTransition transition = transitions[i];
                if (transition.destinationState != destination)
                {
                    continue;
                }

                AnimatorCondition[] conditions = transition.conditions;
                if (conditions.Length == 0)
                {
                    // 条件なしで作られた手動遷移を、対象Triggerで起動する安全な遷移へ修復する。
                    transition.hasExitTime = false;
                    transition.duration = 0f;
                    transition.canTransitionToSelf = canRestartOnHit;
                    transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
                    EditorUtility.SetDirty(transition);
                    return;
                }

                for (int conditionIndex = 0; conditionIndex < conditions.Length; conditionIndex++)
                {
                    if (conditions[conditionIndex].parameter == triggerName)
                    {
                        transition.hasExitTime = false;
                        transition.duration = 0f;
                        transition.canTransitionToSelf = canRestartOnHit;
                        EditorUtility.SetDirty(transition);
                        return;
                    }
                }
            }

            AnimatorStateTransition created = stateMachine.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.duration = 0f;
            created.canTransitionToSelf = canRestartOnHit;
            created.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static void EnsureReturnTransition(AnimatorState source, AnimatorState idleState)
        {
            AnimatorStateTransition[] transitions = source.transitions;
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].destinationState == idleState)
                {
                    transitions[i].hasExitTime = true;
                    transitions[i].exitTime = 1f;
                    transitions[i].duration = 0f;
                    EditorUtility.SetDirty(transitions[i]);
                    return;
                }
            }

            AnimatorStateTransition created = source.AddTransition(idleState);
            created.hasExitTime = true;
            created.exitTime = 1f;
            created.duration = 0f;
        }
    }
}
