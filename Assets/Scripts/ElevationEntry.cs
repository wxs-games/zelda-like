using UnityEngine;

public class ElevationEntry : MonoBehaviour
{
    public Collider2D[] mountainColliders;
    public Collider2D[] mountainBoundary;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            collision.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 15;
            foreach (Collider2D mountain in mountainColliders)
            {
                mountain.enabled = false;
            }
            foreach (Collider2D boundary in mountainBoundary)
            {
                boundary.enabled = true;
            }
        }
    }
}
