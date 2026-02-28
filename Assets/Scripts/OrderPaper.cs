using UnityEngine;
using TMPro;

public class OrderPaper : MonoBehaviour
{
    public TextMeshPro titleText;
    public TextMeshPro descriptionText;
    public TextMeshPro rewardText;

    Order order;

    public void Initialize(Order o)
    {
        order = o;
        if (titleText != null) titleText.text = o.orderName;
        if (descriptionText != null) descriptionText.text = o.description;
        if (rewardText != null) rewardText.text = $"Reward: {o.rewardedGold} Gold";
    }
}