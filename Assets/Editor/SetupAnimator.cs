using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class SetupAnimator
{
    [MenuItem("Tools/Setup Player Animator")]
    public static void SetupPlayerAnimator()
    {
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found in scene! Make sure the Player prefab is loaded with the 'Player' tag.");
            return;
        }

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator not found on Player!");
            return;
        }

        // Must get the actual asset to modify it
        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            Debug.LogError("Animator does not have a valid AnimatorController! Ensure you are not using an override controller or it's missing.");
            return;
        }

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
            Debug.Log("Added 'UltimateAttack' trigger parameter.");
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
            Debug.LogError("'Humanattack2' state not found in the base layer! Please drag the animation into the animator first.");
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
            
            Debug.Log("Created transition: AnyState -> Humanattack2.");
            
            // Return transition when animation finishes
            AnimatorStateTransition exitTrans = attack2State.AddExitTransition();
            exitTrans.hasExitTime = true;
            exitTrans.exitTime = 0.9f;
            exitTrans.duration = 0.2f;
            Debug.Log("Created return transition: Humanattack2 -> Exit.");
        }
        else
        {
            Debug.Log("Transitions already set up correctly.");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>Animator setup complete!</color>");
    }
}
