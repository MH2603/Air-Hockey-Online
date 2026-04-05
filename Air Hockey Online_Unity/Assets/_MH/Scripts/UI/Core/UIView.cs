using UnityEngine;

namespace MH.UI
{
    public class UIView : MonoBehaviour
    {
        public virtual UILayer Layer => UILayer.HUD;

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        public bool IsVisible => gameObject.activeSelf;
    }
}
