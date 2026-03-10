using UnityEngine;

public class OptionButton : MonoBehaviour, IInteractable
{
    public string optionValue;
    string promptLabel;
    BubbleMachine machine;

    public void Initialize(BubbleMachine owner, string value, string label = null)
    {
        machine = owner;
        optionValue = value;
        promptLabel = string.IsNullOrWhiteSpace(label) ? value : label;
    }

    public string GetInteractPrompt()
    {
        if (string.IsNullOrWhiteSpace(promptLabel))
            return "Press E to Select Response";

        return "Press E - " + promptLabel;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (machine != null)
            machine.OptionSelected(optionValue);
    }
}
