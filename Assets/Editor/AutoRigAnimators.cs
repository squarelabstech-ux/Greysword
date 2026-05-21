using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;

[InitializeOnLoad]
public class AutoRigAnimators
{
    static AutoRigAnimators()
    {
        EditorApplication.delayCall += () => RigControllers();
    }

    public static void RigControllers(bool force = false)
    {
        if (!force && SessionState.GetBool("AnimatorsRigged", false)) return;
        SessionState.SetBool("AnimatorsRigged", true);

        RigPlayerController();
        RigEnemyController();
        Debug.Log("[AutoRigAnimators] Successfully analyzed and re-linked Animator Controllers!");
    }

    static void AddParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
    {
        if (!controller.parameters.Any(p => p.name == paramName))
        {
            controller.AddParameter(paramName, type);
        }
    }

    static void AddAnyStateTrigger(AnimatorStateMachine sm, AnimatorState dest, string triggerName)
    {
        if (dest == null) return;
        if (sm.anyStateTransitions.Any(t => t.destinationState == dest)) return; // already exists

        var trans = sm.AddAnyStateTransition(dest);
        trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        trans.duration = 0.1f;
    }

    static void AddReturnTransition(AnimatorState src, AnimatorState dest)
    {
        if (src == null || dest == null) return;
        if (src.transitions.Any(t => t.destinationState == dest)) return; // already exists

        var trans = src.AddTransition(dest);
        trans.hasExitTime = true;
        trans.exitTime = 0.8f; // wait until 80% complete
        trans.duration = 0.15f;
    }

    static void RigPlayerController()
    {
        string path = "Assets/PlayerAnimationController.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) return;

        AddParameter(controller, "isWalking", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "isRunning", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "isJumping", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "hasWeapon", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Damage", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Dead", AnimatorControllerParameterType.Trigger);

        var rootSM = controller.layers[0].stateMachine;

        AnimatorState idleState = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("idle") && !s.state.name.ToLower().Contains("combat")).state;
        AnimatorState walkState = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("walk")).state;
        AnimatorState runState = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("run")).state;
        AnimatorState combatIdle = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("combatidle") || (s.state.name.ToLower().Contains("combat") && s.state.name.ToLower().Contains("idle"))).state;
        AnimatorState combatAttack = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("attack")).state;
        AnimatorState combatDamage = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("damage")).state;
        AnimatorState deadState = rootSM.states.FirstOrDefault(s => s.state.name.ToLower().Contains("dead") || s.state.name.ToLower().Contains("death")).state;

        AddAnyStateTrigger(rootSM, combatAttack, "Attack");
        AddReturnTransition(combatAttack, combatIdle ?? idleState);

        AddAnyStateTrigger(rootSM, combatDamage, "Damage");
        AddReturnTransition(combatDamage, combatIdle ?? idleState);

        AddAnyStateTrigger(rootSM, deadState, "Dead");

        // Link idle to combat idle
        if (idleState != null && combatIdle != null)
        {
            if (!idleState.transitions.Any(t => t.destinationState == combatIdle))
            {
                var trans = idleState.AddTransition(combatIdle);
                trans.AddCondition(AnimatorConditionMode.If, 0, "hasWeapon");
                trans.duration = 0.1f;
            }
            if (!combatIdle.transitions.Any(t => t.destinationState == idleState))
            {
                var trans = combatIdle.AddTransition(idleState);
                trans.AddCondition(AnimatorConditionMode.IfNot, 0, "hasWeapon");
                trans.duration = 0.1f;
            }
            // Let combat idle transition to walk
            if (walkState != null && !combatIdle.transitions.Any(t => t.destinationState == walkState))
            {
                var trans = combatIdle.AddTransition(walkState);
                trans.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
                trans.duration = 0.1f;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void RigEnemyController()
    {
        string path = "Assets/EnemyAI.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) return;

        AddParameter(controller, "Damage", AnimatorControllerParameterType.Trigger);

        var rootSM = controller.layers[0].stateMachine;
        AnimatorState damageState = rootSM.states.FirstOrDefault(s => s.state.name.Contains("Damage")).state;
        AnimatorState chaseState = rootSM.states.FirstOrDefault(s => s.state.name.Contains("Chase")).state;

        AddAnyStateTrigger(rootSM, damageState, "Damage");
        AddReturnTransition(damageState, chaseState);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }
}
