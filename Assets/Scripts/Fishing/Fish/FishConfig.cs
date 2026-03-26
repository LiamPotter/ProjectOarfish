using UnityEngine;

namespace Fishing.Fish
{
    [CreateAssetMenu(fileName = "FishConfig", menuName = "Oarfish/FishConfig")]
    public class FishConfig : ScriptableObject
    {
        [field: SerializeField] public FishBehaviourValues Behaviour { get; private set; }
    }
}
