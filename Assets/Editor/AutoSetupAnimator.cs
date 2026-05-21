using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

[InitializeOnLoad]
public class AutoSetupAnimator
{
    static AutoSetupAnimator()
    {
        EditorApplication.delayCall += DoSetup;
    }

    static void DoSetup()
    {
        // Load the specific controller
        string path = "Assets/PlayerAnimationController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        
        if (controller == null)
        {
            return; // Not found, skip
        }

        bool changed = false;

        // Add UltimateAttack parameter if it doesn't exist
        bool hasParam = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "UltimateAttack")
            {
                hasParam = true;
                break;
            }
        }

        if (!hasParam)
        {
            controller.AddParameter("UltimateAttack", AnimatorControllerParameterType.Trigger);
            Debug.Log("[AutoSetupAnimator] Added 'UltimateAttack' trigger parameter.");
            changed = true;
        }

        // Find Humanattack2 state in base layer
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        AnimatorState attack2State = null;

        foreach (var state in rootStateMachine.states)
        {
            if (state.state.name == "Humanattack2") 
            {
                attack2State = state.state;
                break;
            }
        }

        if (attack2State == null)
        {
            Debug.LogError("[AutoSetupAnimator] 'Humanattack2' state not found in the base layer!");
            return;
        }

        // Create AnyState transition to Humanattack2
        bool hasTransition = false;
        foreach (var trans in rootStateMachine.anyStateTransitions)
        {
            if (trans.destinationState == attack2State)
            {
                hasTransition = true;
                break;
            }
        }

        if (!hasTransition)
        {
            AnimatorStateTransition newTrans = rootStateMachine.AddAnyStateTransition(attack2State);
            newTrans.AddCondition(AnimatorConditionMode.If, 0, "UltimateAttack");
            newTrans.duration = 0.1f;
            newTrans.hasExitTime = false;
            
            Debug.Log("[AutoSetupAnimator] Created transition: AnyState -> Humanattack2.");
            
            // Return transition when animation finishes
            AnimatorStateTransition exitTrans = attack2State.AddExitTransition();
            exitTrans.hasExitTime = true;
            exitTrans.exitTime = 0.9f;
            exitTrans.duration = 0.2f;
            Debug.Log("[AutoSetupAnimator] Created return transition: Humanattack2 -> Exit.");
            
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[AutoSetupAnimator] Animator setup successfully injected! The UltimateAttack trigger and transitions are now applied.</color>");
        }
    }
}
