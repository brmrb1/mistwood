using UnityEngine;

// 挂载到“拖拽成功后实例化出的图片”上
public class FeedbackClickable : MonoBehaviour
{
    // 记录生成这个图片的原始拖拽物品组件
    public dragright ownerDragright;

    private void Awake()
    {
        // 自动为生成的图片添加点击盒（如果它没有）
        Collider2D col2d = GetComponent<Collider2D>();
        if (col2d == null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                if (sr.sprite != null)
                {
                    box.size = sr.sprite.bounds.size;
                    box.offset = sr.sprite.bounds.center;
                }
                box.isTrigger = false;
            }
        }
    }

    private void OnMouseUpAsButton()
    {
        // 点击后，通知原来的拖拽物体清除实例出来的图并恢复重置自身
        if (ownerDragright != null)
        {
            ownerDragright.ClearSpawnedAndReset();
        }
    }
}

