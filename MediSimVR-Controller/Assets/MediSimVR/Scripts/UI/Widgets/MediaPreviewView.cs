using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MediSimVR
{
    // The content preview card (Send tab and Play page): picture / type icon / text paper, duration badge, name, info,
    // Change / Edit (text) / Remove. Empty state when no content is chosen. The owner wires the three buttons.
    public class MediaPreviewView : MonoBehaviour
    {
        public GameObject filledGroup, emptyGroup, badge, textPaper;
        public UIGradient previewBackground;
        public Image previewImage, previewIcon;
        public TMP_Text previewText, badgeText, nameText, infoText;
        public Button changeButton, editButton, removeButton;
        public Sprite videoIcon, audioIcon, imageIcon, textIcon;

        private Sprite imageSprite;

        public void Bind(MediaFile f)
        {
            filledGroup.SetActive(f != null);
            emptyGroup.SetActive(f == null);
            if (f == null) return;

            nameText.text = f.Name;
            bool timed = (f.Kind == MediaKind.Video || f.Kind == MediaKind.Audio) && f.DurationSec >= 0;
            infoText.text = MediaTypes.Label(f.Kind) + "  •  " + Fmt.Size(f.SizeBytes) + (timed ? "  •  " + Fmt.Dur(f.DurationSec) : "");
            previewBackground.SetColors(f.ThumbTop, f.ThumbBottom);

            bool isImage = f.Kind == MediaKind.Image && f.Preview != null;
            if (isImage)
            {
                if (imageSprite == null || imageSprite.texture != f.Preview)
                    imageSprite = Sprite.Create(f.Preview, new Rect(0, 0, f.Preview.width, f.Preview.height), new Vector2(0.5f, 0.5f));
                previewImage.sprite = imageSprite;
            }
            previewImage.gameObject.SetActive(isImage);

            bool isText = f.Kind == MediaKind.Text;
            textPaper.SetActive(isText);
            if (isText) previewText.text = string.IsNullOrEmpty(f.Text) ? "(empty text)" : f.Text;

            previewIcon.gameObject.SetActive(!isImage && !isText);
            previewIcon.sprite = f.Kind == MediaKind.Video ? videoIcon : f.Kind == MediaKind.Audio ? audioIcon : f.Kind == MediaKind.Image ? imageIcon : textIcon;
            badge.SetActive(timed);
            badgeText.text = Fmt.Dur(f.DurationSec);
            editButton.gameObject.SetActive(isText);
        }
    }
}
