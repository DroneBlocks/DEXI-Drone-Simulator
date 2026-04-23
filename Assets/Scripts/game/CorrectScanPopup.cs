using TMPro;
using UnityEngine;

public class CorrectScanPopup : MonoBehaviour
{
    [SerializeField]
    private TMP_Text completedObjectiveText;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
        {
            Destroy(gameObject);
        }
    }

    public void SetObjectiveText(string text)
    {
        completedObjectiveText.text = text;
    }
}
