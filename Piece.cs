using Godot;

public partial class Piece : RigidBody2D
{
	[Export]
	public int Level = 1;

	public bool IsMerged = false;
	public bool HasDropped = false; // Parça oyuncunun elinden bırakıldı mı?

	private static readonly Color[] LevelColors = new Color[]
	{
		new Color(0.7f, 0.7f, 0.7f), // L1: Hurda Metal
		new Color(0.3f, 0.7f, 1.0f), // L2: Vida
		new Color(0.9f, 0.6f, 0.2f), // L3: Dişli
		new Color(0.2f, 0.8f, 0.4f), // L4: Mekanizma
		new Color(0.9f, 0.2f, 0.2f), // L5: Motor
		new Color(0.8f, 0.2f, 0.9f), // L6: Enerji Çekirdeği
		new Color(1.0f, 0.8f, 0.1f), // L7: Robot Gövdesi
		new Color(0.1f, 0.9f, 0.9f)  // L8: Robot
	};

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		UpdateAppearance();
	}

	public void SetLevel(int newLevel)
	{
		Level = newLevel;
		UpdateAppearance();
	}

	private void UpdateAppearance()
	{
		float scaleFactor = 0.35f + (Level - 1) * 0.12f;
		
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		if (sprite != null)
		{
			sprite.Scale = new Vector2(scaleFactor, scaleFactor);
			int colorIndex = Mathf.Clamp(Level - 1, 0, LevelColors.Length - 1);
			sprite.Modulate = LevelColors[colorIndex];
		}

		if (collisionShape != null && collisionShape.Shape is CircleShape2D circle)
		{
			circle.Radius = 22f + (Level - 1) * 7.5f;
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
		sprite.Scale = Vector2.Zero; // Başlangıçta görünmez/nokta gibi başla

		var tween = CreateTween();
		// 0.2 saniyede yaylanarak hedef boyutuna gelsin
		tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(sprite, "scale", targetScale, 0.2f);
	}
}
