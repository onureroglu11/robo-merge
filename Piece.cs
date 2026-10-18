using Godot;

public partial class Piece : RigidBody2D
{
	[Export]
	public int Level = 1;

	public bool IsMerged = false;
	public bool HasDropped = false;

	// Kutuya göre seviye taban boyutları (L1 -> L8)
	private static readonly float[] LevelRadii = new float[]
	{
		35f,   // L1
		48f,   // L2
		64f,   // L3
		82f,   // L4
		104f,  // L5
		128f,  // L6
		152f,  // L7
		178f   // L8
	};

	private static readonly float[] ColliderMultipliers = new float[]
	{
		0.81f, // L1
		0.69f, // L2
		0.74f, // L3
		0.79f, // L4
		0.89f, // L5
		0.77f, // L6
		0.70f, // L7
		1.0f   // L8 poligon kullandığı için çarpan gerektirmez
	};

	private static readonly Vector2[] SpriteOffsets = new Vector2[]
	{
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		new Vector2(4f, 2f), // L7 ofseti
		Vector2.Zero         // L8 poligonla merkezli
	};

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collisionShape != null)
		{
			collisionShape.Shape = new CircleShape2D();
		}

		UpdateAppearance();
	}

	public void SetLevel(int newLevel)
	{
		Level = Mathf.Clamp(newLevel, 1, 8);
		UpdateAppearance();
	}

	private void UpdateAppearance()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		var circleCol = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		var polyCol = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");

		float targetRadius = LevelRadii[Level - 1];
		float scale = (targetRadius * 2f) / 256f;

		// 1. Çarpışma Şeklini Seviyeye Göre Ayarla
		if (Level == 8 && polyCol != null)
		{
			// L8 için daireyi kapat ve gizle
			if (circleCol != null)
			{
				circleCol.Disabled = true;
				circleCol.Visible = false;
			}

			// L8 için poligonu aç ve göster
			polyCol.Disabled = false;
			polyCol.Visible = true;
			polyCol.Scale = new Vector2(scale, scale);
		}
		else
		{
			// L1 - L7 için daireyi aç ve göster
			if (circleCol != null)
			{
				circleCol.Disabled = false;
				circleCol.Visible = true;

				if (circleCol.Shape is CircleShape2D circle)
				{
					circle.Radius = targetRadius * ColliderMultipliers[Level - 1];
				}
			}

			// L1 - L7 için robot poligonunu kapat ve gizle
			if (polyCol != null)
			{
				polyCol.Disabled = true;
				polyCol.Visible = false;
			}
		}

		// 2. Görseli Yükle ve Boyutlandır
		if (sprite != null)
		{
			string path = $"res://sprites/L{Level}.png";
			if (ResourceLoader.Exists(path))
			{
				sprite.Texture = GD.Load<Texture2D>(path);
			}

			sprite.Modulate = Colors.White;
			sprite.Position = SpriteOffsets[Level - 1];
			sprite.Scale = new Vector2(scale, scale);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body is Piece otherPiece)
		{
			if (IsMerged || otherPiece.IsMerged || Level != otherPiece.Level)
				return;

			if (GetInstanceId() > otherPiece.GetInstanceId())
				return;

			IsMerged = true;
			otherPiece.IsMerged = true;

			Vector2 spawnPos = (GlobalPosition + otherPiece.GlobalPosition) / 2f;

			var main = GetTree().CurrentScene as Main;
			if (main != null)
			{
				main.MergePieces(Level + 1, spawnPos);
			}

			QueueFree();
			otherPiece.QueueFree();
		}
	}

	public void PlaySpawnAnimation()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null) return;

		Vector2 targetScale = sprite.Scale;
		sprite.Scale = Vector2.Zero;

		var tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(sprite, "scale", targetScale, 0.2f);
	}
}
