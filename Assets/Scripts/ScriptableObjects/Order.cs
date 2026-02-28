using UnityEngine; 
using System.Collections.Generic; 
[System.Serializable] 
public class RequiredItem 
{ 
    //public ItemType itemType; 
    public int requiredAmount; 
} 

[CreateAssetMenu(fileName = "Order", menuName = "Scriptable Objects/Order")] 
public class Order : ScriptableObject { [Header("Order Metadata")] 
    public string orderName; [TextArea] 
    public string description; [Header("Order Requirements")] 
    public float requiredGold; public List<RequiredItem> requiredItems; 
    //public List<SlabMaterial> requiredMaterials; [Header("Order Rewards")] 
    public float rewardedGold; public List<GameObject> rewardedItems; 
    
    //public List<SlabMaterial> rewardedMaterials; 
    
    public string customRewardText; 
    public GameObject originPin; 
    // runtime state
    
    public bool isQuota = false; 
    public bool assignedToSocket = false; 
    public bool completed = false; 

}