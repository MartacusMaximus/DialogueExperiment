using UnityEngine;

public class OrderSpawner : MonoBehaviour
{
    public SpeechBubbleEntity bubblePrefab;
    public Transform spawnParent;
    public float radius = 0.5f;

    public void SpawnOrderBubble(Order order)
    {
        if (bubblePrefab == null || order == null) return;
        Vector3 pos = transform.position + Random.insideUnitSphere * radius;
        pos.y = transform.position.y + 1.0f;

        var go = Instantiate(bubblePrefab.gameObject, pos, Quaternion.identity, spawnParent).GetComponent<SpeechBubbleEntity>();
        go.InitializeGenerated(
            $"LEGACY_ORDER_{order.name}",
            order.orderName,
            $"{order.orderName}\nReward: {order.rewardedGold} Gold"
        );
        go.PlayInflate();
    }
}
