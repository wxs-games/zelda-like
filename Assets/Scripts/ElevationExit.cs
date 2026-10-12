using UnityEngine;

public class ElevationExit : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("montainBody")]
    public Collider2D[] mountainBody;
    [UnityEngine.Serialization.FormerlySerializedAs("montainBoundary")]
    public Collider2D[] mountainBoundary;

    void OnTriggerEnter2D(Collider2D co)
    {
        if (co.gameObject.tag == "Player")
        {
            co.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 5;
            foreach (Collider2D body in mountainBody)
            {

                body.enabled = true;

            }
            foreach (Collider2D boundary in mountainBoundary)
            {
                boundary.enabled = false;
            }
        }
    }
}
