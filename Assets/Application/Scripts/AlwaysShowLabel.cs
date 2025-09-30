using UnityEngine;

public class AlwaysShowLabel : MonoBehaviour
{
    [SerializeField] private GameObject seeItSayItLabel;

    void Start()
    {
        GameObject child1 = transform.GetChild(0).gameObject;
        GameObject child2 = transform.GetChild(1).gameObject;
        if (seeItSayItLabel != null)
        {
            seeItSayItLabel.SetActive(true);

            // CanvasGroupが付いていたら透明度も固定
            var cg = seeItSayItLabel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
    }
}
