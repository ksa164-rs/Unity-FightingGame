using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    [CreateAssetMenu(
        fileName = "AnimationProfile_New",
        menuName = "GASG/Fighting/Animation Profile")]
    public sealed class FighterAnimationProfile : ScriptableObject
    {
        [Header("ループ状態")]
        [SerializeField] private FighterActionDefinition idle;
        [SerializeField] private FighterActionDefinition crouchIdle;

        [Header("単発アクション")]
        [SerializeField] private FighterActionDefinition jump;
        [SerializeField] private FighterActionDefinition land;
        [SerializeField] private FighterActionDefinition forwardStep;
        [SerializeField] private FighterActionDefinition backwardStep;
        [SerializeField] private FighterActionDefinition block;
        [SerializeField] private FighterActionDefinition hit;
        [SerializeField] private FighterActionDefinition knockdown;
        [SerializeField] private FighterActionDefinition thrown;
        [SerializeField] private FighterActionDefinition reset;

        public FighterActionDefinition Idle => idle;
        public FighterActionDefinition CrouchIdle => crouchIdle;
        public FighterActionDefinition Jump => jump;
        public FighterActionDefinition Land => land;
        public FighterActionDefinition ForwardStep => forwardStep;
        public FighterActionDefinition BackwardStep => backwardStep;
        public FighterActionDefinition Block => block;
        public FighterActionDefinition Hit => hit;
        public FighterActionDefinition Knockdown => knockdown;
        public FighterActionDefinition Thrown => thrown;
        public FighterActionDefinition Reset => reset;

        public IEnumerable<FighterActionDefinition> TriggerActions
        {
            get
            {
                yield return jump;
                yield return land;
                yield return forwardStep;
                yield return backwardStep;
                yield return block;
                yield return hit;
                yield return knockdown;
                yield return thrown;
                yield return reset;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            FighterActionDefinition newIdle,
            FighterActionDefinition newCrouchIdle,
            FighterActionDefinition newJump,
            FighterActionDefinition newLand,
            FighterActionDefinition newForwardStep,
            FighterActionDefinition newBackwardStep,
            FighterActionDefinition newBlock,
            FighterActionDefinition newHit,
            FighterActionDefinition newKnockdown,
            FighterActionDefinition newThrown,
            FighterActionDefinition newReset)
        {
            idle = newIdle;
            crouchIdle = newCrouchIdle;
            jump = newJump;
            land = newLand;
            forwardStep = newForwardStep;
            backwardStep = newBackwardStep;
            block = newBlock;
            hit = newHit;
            knockdown = newKnockdown;
            thrown = newThrown;
            reset = newReset;
        }

        public void EditorFillMissing(
            FighterActionDefinition newIdle,
            FighterActionDefinition newCrouchIdle,
            FighterActionDefinition newJump,
            FighterActionDefinition newLand,
            FighterActionDefinition newForwardStep,
            FighterActionDefinition newBackwardStep,
            FighterActionDefinition newBlock,
            FighterActionDefinition newHit,
            FighterActionDefinition newKnockdown,
            FighterActionDefinition newThrown,
            FighterActionDefinition newReset)
        {
            idle ??= newIdle;
            crouchIdle ??= newCrouchIdle;
            jump ??= newJump;
            land ??= newLand;
            forwardStep ??= newForwardStep;
            backwardStep ??= newBackwardStep;
            block ??= newBlock;
            hit ??= newHit;
            knockdown ??= newKnockdown;
            thrown ??= newThrown;
            reset ??= newReset;
        }
#endif
    }
}
