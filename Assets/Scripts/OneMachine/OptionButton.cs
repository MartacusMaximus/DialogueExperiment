using UnityEngine;

public class OptionButton : MonoBehaviour, IInteractable
{
    public string optionValue;
    MessageMachine machine;

    public void Initialize(MessageMachine owner, string value)
    {
        machine = owner;
        optionValue = value;
    }

    public string GetInteractPrompt()
    {
        return "Press E - " + optionValue;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (machine != null)
            machine.OptionSelected(optionValue);
    }
}