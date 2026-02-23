using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        CharacterStock stock = other.GetComponentInParent<CharacterStock>();

        if (stock != null)
        {
            stock.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
        }
    }
}
