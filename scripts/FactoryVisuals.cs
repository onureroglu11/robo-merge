using Godot;

namespace MergeRobotB;

// Fabrika arka planlarını ve konveyör bantlarını çizen yardımcı sınıf
[Tool]
public partial class FactoryVisuals : Node2D
{
	public enum VisualKind
	{
		Background,
		CartoonWheelConveyor,
		RedRailConveyor
	}

	[Export] public VisualKind Kind = VisualKind.Background;
	[Export] public Vector2 BeltSize = new(220, 48);
	public float AnimOffset = 0f;
	[Export] public bool ShowRobotExitHint = false;

	// Konveyörün şu an dönüp dönmediğini ve hızını kontrol eder
	[Export] public bool IsRunning = true;
	[Export] public float BeltSpeed = 48f;

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;
		if (Kind != VisualKind.Background && IsRunning)
		{
			AnimOffset = (AnimOffset + (float)delta * BeltSpeed) % 1000f;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		switch (Kind)
		{
			case VisualKind.Background:
				if (GameData.IsLightTheme)
					DrawLightFactoryPipesBg();
				else
					DrawDarkRivetedWallBg();
				break;

			case VisualKind.CartoonWheelConveyor:
				DrawCartoonWheelConveyor();
				break;

			case VisualKind.RedRailConveyor:
				DrawRedRailConveyor();
				break;
		}
	}

	// Karanlık tema: Perçinli metal plakalar ve ortada kırmızı fabrika şeridi
	private void DrawDarkRivetedWallBg()
	{
		Color topWall = new(0.28f, 0.31f, 0.38f);
		Color bottomWall = new(0.18f, 0.21f, 0.27f);
		Color seamColor = new(0.11f, 0.13f, 0.17f, 0.85f);
		Color highlightSeam = new(0.40f, 0.44f, 0.52f, 0.45f);
		Color rivetShadow = new(0.08f, 0.10f, 0.14f);
		Color rivetHead = new(0.38f, 0.42f, 0.50f);

		DrawRect(new Rect2(0, 0, 720, 740), topWall);
		DrawRect(new Rect2(0, 740, 720, 540), bottomWall);

		// Ortadaki yatay kırmızı-bordo şerit
		DrawRect(new Rect2(0, 700, 720, 38), new Color(0.50f, 0.14f, 0.15f));
		DrawRect(new Rect2(0, 738, 720, 38), new Color(0.24f, 0.07f, 0.08f));

		// Şerit üzerindeki eğik gri kesikler
		float[] slashGroups = { 210f, 460f };
		foreach (float sx in slashGroups)
		{
			for (int k = 0; k < 2; k++)
			{
				float x0 = sx + k * 26f;
				Vector2[] topSlash =
				{
					new(x0 + 22f, 700f), new(x0 + 38f, 700f),
					new(x0 + 16f, 738f), new(x0, 738f)
				};
				Vector2[] botSlash =
				{
					new(x0 + 16f, 738f), new(x0, 738f),
					new(x0 - 22f, 776f), new(x0 - 6f, 776f)
				};
				DrawColoredPolygon(topSlash, new Color(0.45f, 0.50f, 0.58f));
				DrawColoredPolygon(botSlash, new Color(0.26f, 0.30f, 0.38f));
			}
		}

		// Metal duvar plakaları ve köşe perçinleri
		Rect2[] plates =
		{
			new(20, 40, 210, 260), new(230, 40, 260, 200), new(490, 40, 210, 300),
			new(20, 300, 210, 280), new(230, 240, 260, 340), new(490, 340, 210, 240),
			new(20, 580, 210, 310), new(230, 580, 260, 280), new(490, 580, 210, 310),
			new(20, 890, 210, 350), new(230, 860, 260, 380), new(490, 890, 210, 350)
		};

		foreach (var r in plates)
		{
			DrawRect(r, seamColor, false, 3.0f);
			DrawLine(new Vector2(r.Position.X + 3, r.Position.Y + 3), new Vector2(r.End.X - 3, r.Position.Y + 3), highlightSeam, 2.0f);

			Vector2[] corners =
			{
				new(r.Position.X + 12, r.Position.Y + 12),
				new(r.End.X - 12, r.Position.Y + 12),
				new(r.Position.X + 12, r.End.Y - 12),
				new(r.End.X - 12, r.End.Y - 12)
			};
			foreach (var c in corners)
			{
				DrawCircle(c + new Vector2(1.5f, 2f), 4.2f, rivetShadow);
				DrawCircle(c, 3.8f, rivetHead);
			}
		}

		// Ekran kenarındaki sekizgen metal çerçeve
		Color frameColor = new(0.36f, 0.40f, 0.47f);
		Color frameBorder = new(0.12f, 0.14f, 0.18f);
		DrawRect(new Rect2(0, 0, 720, 22), frameColor);
		DrawRect(new Rect2(0, 1258, 720, 22), frameColor);
		DrawRect(new Rect2(0, 0, 22, 1280), frameColor);
		DrawRect(new Rect2(698, 0, 22, 1280), frameColor);

		Vector2[][] chamfers =
		{
			new Vector2[] { new(0, 0), new(95, 0), new(0, 95) },
			new Vector2[] { new(720, 0), new(625, 0), new(720, 95) },
			new Vector2[] { new(0, 1280), new(95, 1280), new(0, 1185) },
			new Vector2[] { new(720, 1280), new(625, 1280), new(720, 1185) }
		};
		foreach (var poly in chamfers)
		{
			DrawColoredPolygon(poly, frameColor);
			DrawLine(poly[1], poly[2], frameBorder, 5.0f);
		}
	}

	// Aydınlık tema: Açık gri-yeşil fabrika boruları, kazan ve yürüyüş köprüsü
	private void DrawLightFactoryPipesBg()
	{
		Color wallBg = new(0.48f, 0.58f, 0.56f);
		Color farPipe = new(0.41f, 0.49f, 0.48f);
		Color midStruct = new(0.33f, 0.37f, 0.37f);
		Color darkStruct = new(0.25f, 0.28f, 0.28f);
		Color lightTankHighlight = new(0.68f, 0.72f, 0.72f);

		DrawRect(new Rect2(0, 0, 720, 1280), wallBg);

		// Arka plandaki dikey borular
		float[] bgPipesX = { 85f, 595f };
		foreach (float px in bgPipesX)
		{
			DrawRect(new Rect2(px, 0, 44, 1280), farPipe);
			for (int y = 60; y < 1260; y += 110)
			{
				DrawRect(new Rect2(px - 6, y, 56, 8), farPipe.Lightened(0.08f));
			}
		}

		// Sağ ve soldaki kalın ana borular
		float[] mainPipesX = { 135f, 640f };
		foreach (float mx in mainPipesX)
		{
			DrawRect(new Rect2(mx, 90, 54, 730), midStruct);
			DrawRect(new Rect2(mx + 8, 90, 14, 730), midStruct.Lightened(0.08f));
			for (int y = 170; y < 800; y += 120)
			{
				DrawRect(new Rect2(mx - 6, y, 66, 10), midStruct.Lightened(0.12f));
			}
		}

		DrawRect(new Rect2(0, 130, 720, 18), darkStruct);

		// Ortadaki büyük fabrika kazanı / silosu
		DrawRect(new Rect2(235, 0, 230, 310), midStruct);
		DrawRect(new Rect2(290, 0, 65, 310), lightTankHighlight);
		DrawRect(new Rect2(355, 0, 45, 310), midStruct.Lightened(0.14f));
		DrawRect(new Rect2(225, 300, 250, 28), darkStruct);
		DrawRect(new Rect2(275, 328, 150, 495), midStruct.Darkened(0.06f));

		// Kazanın yanındaki bakım merdiveni
		float ladderX = 258f;
		DrawLine(new Vector2(ladderX, 330), new Vector2(ladderX, 820), lightTankHighlight, 3f);
		DrawLine(new Vector2(ladderX + 18, 330), new Vector2(ladderX + 18, 820), lightTankHighlight, 3f);
		for (int ly = 345; ly < 815; ly += 22)
		{
			DrawLine(new Vector2(ladderX, ly), new Vector2(ladderX + 18, ly), lightTankHighlight, 2.5f);
		}

		// Yatay metal köprü ve kafes kirişleri
		DrawRect(new Rect2(0, 820, 720, 16), darkStruct);
		DrawRect(new Rect2(0, 852, 720, 14), darkStruct);
		for (int tx = 0; tx < 720; tx += 36)
		{
			DrawLine(new Vector2(tx, 836), new Vector2(tx + 18, 852), darkStruct, 3.5f);
			DrawLine(new Vector2(tx + 18, 852), new Vector2(tx + 36, 836), darkStruct, 3.5f);
		}

		// Köprü üzerindeki variller
		float[] barrelXs = { 35f, 70f, 385f, 420f, 540f, 575f };
		foreach (float bx in barrelXs)
		{
			DrawRect(new Rect2(bx, 768, 28, 52), darkStruct);
			DrawLine(new Vector2(bx - 2, 784), new Vector2(bx + 30, 784), midStruct.Lightened(0.1f), 3f);
			DrawLine(new Vector2(bx - 2, 802), new Vector2(bx + 30, 802), midStruct.Lightened(0.1f), 3f);
		}

		DrawRect(new Rect2(20, 930, 180, 350), midStruct);
		DrawRect(new Rect2(520, 930, 180, 350), midStruct);
		DrawRect(new Rect2(290, 960, 110, 320), darkStruct);
		DrawRect(new Rect2(420, 960, 110, 320), darkStruct);
	}

	// Sol üstteki 3 çarklı konveyör (Sadece yeni parça gelirken döner, parça durduğunda o da durur)
	private void DrawCartoonWheelConveyor()
	{
		float w = BeltSize.X;
		float h = BeltSize.Y;
		float r = h * 0.5f;

		Color outlineDark = new(0.06f, 0.11f, 0.13f);
		Color beltDark = new(0.12f, 0.22f, 0.26f);
		Color treadLight = new(0.48f, 0.76f, 0.82f);
		Color wheelTeal = new(0.32f, 0.58f, 0.62f);
		Color hubLight = new(0.56f, 0.80f, 0.84f);

		// Dış kapsül gövde
		DrawCircle(new Vector2(r, r), r, outlineDark);
		DrawCircle(new Vector2(w - r, r), r, outlineDark);
		DrawRect(new Rect2(r, 0, w - h, h), outlineDark);

		// İç koyu bant alanı
		float inR = r - 4f;
		DrawCircle(new Vector2(r, r), inR, beltDark);
		DrawCircle(new Vector2(w - r, r), inR, beltDark);
		DrawRect(new Rect2(r, 4, w - h, h - 8), beltDark);

		// Çarkları birleştiren iç şerit
		DrawRect(new Rect2(r, r - 8f, w - h, 16f), wheelTeal);
		DrawLine(new Vector2(r, r - 6f), new Vector2(w - r, r - 6f), hubLight, 2.5f);

		// Üstteki dişler sağa (+shift), alttaki dişler sola (-shift) kayar
		float step = 18f;
		float shift = AnimOffset % step;
		for (float x = r - 6f + shift; x < w - r + 4f; x += step)
		{
			DrawRect(new Rect2(x, 3f, 10f, 5f), treadLight);
		}
		for (float x = r - 6f + (step - shift); x < w - r + 4f; x += step)
		{
			DrawRect(new Rect2(x, h - 8f, 10f, 5f), treadLight);
		}

		// 3 adet 5 delikli çark (AnimOffset ile parçanın hızına uyumlu döner)
		float[] wheelXs = { r + 2f, w * 0.5f, w - r - 2f };
		float wheelRadius = r - 7f;
		float rotAngle = AnimOffset / r;

		foreach (float wx in wheelXs)
		{
			Vector2 center = new(wx, r);
			DrawCircle(center, wheelRadius + 3f, outlineDark);
			DrawCircle(center, wheelRadius, wheelTeal);

			for (int i = 0; i < 5; i++)
			{
				float ang = rotAngle + i * (Mathf.Tau / 5f);
				Vector2 holePos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (wheelRadius * 0.58f);
				DrawCircle(holePos, 4.0f, outlineDark);
			}

			DrawCircle(center, wheelRadius * 0.34f, outlineDark);
			DrawCircle(center, wheelRadius * 0.27f, hubLight);
		}
	}

	// Sağ alttaki kırmızı raylı robot tahliye konveyörü
	private void DrawRedRailConveyor()
	{
		float w = BeltSize.X;
		float h = BeltSize.Y;

		Color beltCharcoal = new(0.20f, 0.21f, 0.22f);
		Color ribDark = new(0.14f, 0.15f, 0.16f);
		Color ribLight = new(0.27f, 0.28f, 0.30f);
		Color redRail = new(0.76f, 0.08f, 0.08f);
		Color redHighlight = new(0.95f, 0.32f, 0.32f);
		Color redDeep = new(0.48f, 0.04f, 0.04f);

		DrawRect(new Rect2(0, 6, w, h - 18), beltCharcoal);

		float step = 12f;
		float shift = AnimOffset % step;
		for (float x = shift; x < w; x += step)
		{
			DrawLine(new Vector2(x, 6), new Vector2(x, h - 12), ribDark, 3.0f);
			DrawLine(new Vector2(x + 3f, 6), new Vector2(x + 3f, h - 12), ribLight, 1.5f);
		}

		// Bant üzerindeki yön okları (Yazısız çıkış göstergesi)
		if (ShowRobotExitHint)
		{
			float arrowSpacing = 54f;
			float arrowShift = (AnimOffset * 1.2f) % arrowSpacing;
			Color chevronCol = new(1.0f, 0.78f, 0.22f, 0.75f);
			float midY = 6f + (h - 18f) * 0.5f;

			for (float ax = 30f + arrowShift; ax < w - 20f; ax += arrowSpacing)
			{
				Vector2 p1 = new(ax - 8f, midY - 7f);
				Vector2 p2 = new(ax + 4f, midY);
				Vector2 p3 = new(ax - 8f, midY + 7f);
				DrawLine(p1, p2, chevronCol, 3.5f);
				DrawLine(p2, p3, chevronCol, 3.5f);
			}
		}

		// Üst ve alt kırmızı raylar + perçinler
		DrawRect(new Rect2(0, 0, w, 7), redRail);
		DrawLine(new Vector2(0, 1.5f), new Vector2(w, 1.5f), redHighlight, 2.0f);

		DrawRect(new Rect2(0, h - 13, w, 13), redRail);
		DrawLine(new Vector2(0, h - 12f), new Vector2(w, h - 12f), redHighlight, 2.0f);
		DrawRect(new Rect2(0, h - 4, w, 4), redDeep);

		for (float rx = 22f; rx < w - 12f; rx += 48f)
		{
			Vector2 rPos = new(rx, h - 6.5f);
			DrawCircle(rPos + new Vector2(1f, 1f), 3.2f, redDeep);
			DrawCircle(rPos, 2.8f, new Color(0.82f, 0.85f, 0.88f));
		}
	}
}
