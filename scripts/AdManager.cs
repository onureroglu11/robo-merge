using Godot;
using System;

namespace MergeRobotB;

// Google Play (Android) ve App Store (iOS) için Ödüllü Reklam (Rewarded Ad) yöneticisi.
// Cihazda native AdMob eklentisi kuruluysa gerçek reklam gösterir,
// Masaüstünde (Mac/Windows) veya eklenti yokken yerleşik interaktif ödüllü reklam ekranını çalıştırır.
public partial class AdManager : Node
{
	// Google tarafından sağlanan resmi Test Rewarded Ad Unit ID'leri
	// Mağazaya çıkarken kendi AdMob panelinizden aldığınız ID'leri buraya yazabilirsiniz
	public const string AndroidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
	public const string IosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";

	private GodotObject _nativeAdMobSingleton;
	private bool _isRewardEarned = false;
	private Action _onRewardCallback;
	private Action _onClosedWithoutRewardCallback;

	// Eklenti olmadığında veya masaüstü testlerinde gösterilen tam ekran interaktif reklam arayüzü
	private CanvasLayer _mockAdLayer;
	private Label _countdownLabel;
	private Button _closeAdButton;
	private ProgressBar _progressBar;
	private float _mockAdRemainingSec = 0f;
	private bool _mockAdActive = false;
	private bool _wasMusicEnabledBeforeAd = false;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		InitializeNativePluginIfPresent();
		BuildFallbackAdOverlay();
	}

	private void InitializeNativePluginIfPresent()
	{
		if (Engine.IsEditorHint()) return;

		// Android veya iOS üzerinde resmi AdMob / PoingAdMob singleton'ı varsa otomatik bağlan
		string[] singletonNames = { "PoingGodotAdMob", "AdMob", "GodotAdMob" };
		foreach (string name in singletonNames)
		{
			if (Engine.HasSingleton(name))
			{
				_nativeAdMobSingleton = Engine.GetSingleton(name);
				if (_nativeAdMobSingleton != null && _nativeAdMobSingleton.HasMethod("initialize"))
				{
					_nativeAdMobSingleton.Call("initialize");
				}
				break;
			}
		}
	}

	public string GetPlatformAdUnitId()
	{
		return OS.GetName() == "iOS" ? IosRewardedAdUnitId : AndroidRewardedAdUnitId;
	}

	// Ödüllü reklamı başlatır; oyuncu reklamı bitirip kapattığında onRewardEarned çalışır
	public void ShowRewardedAd(Action onRewardEarned, Action onAdCancelled = null)
	{
		_onRewardCallback = onRewardEarned;
		_onClosedWithoutRewardCallback = onAdCancelled;
		_isRewardEarned = false;

		// Reklam süresince arka plan müziğini geçici olarak sustur (Apple & Google Play politikası)
		_wasMusicEnabledBeforeAd = GameData.MusicEnabled;
		if (_wasMusicEnabledBeforeAd)
		{
			AudioServer.SetBusMute(0, true);
		}

		// Native mobil eklenti yüklüyse ve hazırsa onu çağır
		if (_nativeAdMobSingleton != null && _nativeAdMobSingleton.HasMethod("show_rewarded_ad"))
		{
			_nativeAdMobSingleton.Call("show_rewarded_ad", GetPlatformAdUnitId());
			return;
		}

		// Aksi halde çoklu platform (Mac, Windows, Android, iOS) uyumlu yerleşik ödüllü reklam ekranını aç
		StartInteractiveRewardedAd();
	}

	private void BuildFallbackAdOverlay()
	{
		_mockAdLayer = new CanvasLayer
		{
			Name = "RewardedAdLayer",
			Layer = 30,
			ProcessMode = ProcessModeEnum.Always,
			Visible = false
		};
		AddChild(_mockAdLayer);

		var bg = new ColorRect
		{
			Color = new Color(0.05f, 0.07f, 0.11f, 0.98f),
			Size = new Vector2(720, 1280)
		};
		_mockAdLayer.AddChild(bg);

		// Üst bilgi barı (AdMob standart tasarımına uygun)
		var topBar = new Panel
		{
			Position = new Vector2(24, 32),
			Size = new Vector2(672, 68)
		};
		var topStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.18f, 0.26f),
			CornerRadiusTopLeft = 20,
			CornerRadiusTopRight = 20,
			CornerRadiusBottomLeft = 20,
			CornerRadiusBottomRight = 20
		};
		topBar.AddThemeStyleboxOverride("panel", topStyle);
		bg.AddChild(topBar);

		_countdownLabel = new Label
		{
			Position = new Vector2(24, 14),
			Size = new Vector2(450, 40)
		};
		GameData.ApplySoftFont(_countdownLabel, 22, Colors.White, new Color(0.08f, 0.10f, 0.15f), 6);
		topBar.AddChild(_countdownLabel);

		_closeAdButton = MainMenu.CreateGlossyPillButton(
			new Vector2(525, 10), new Vector2(135, 48),
			new Color(0.24f, 0.82f, 0.44f), new Color(0.12f, 0.48f, 0.24f), 20
		);
		_closeAdButton.Text = "✕";
		_closeAdButton.Visible = false;
		_closeAdButton.Pressed += OnCloseRewardedAdPressed;
		topBar.AddChild(_closeAdButton);

		// Orta tanıtım kartı
		var centerCard = new Panel
		{
			Position = new Vector2(70, 260),
			Size = new Vector2(580, 680)
		};
		var cardStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.16f, 0.24f),
			CornerRadiusTopLeft = 32,
			CornerRadiusTopRight = 32,
			CornerRadiusBottomLeft = 32,
			CornerRadiusBottomRight = 32,
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderWidthRight = 4,
			BorderWidthBottom = 8,
			BorderColor = new Color(1.0f, 0.76f, 0.18f)
		};
		centerCard.AddThemeStyleboxOverride("panel", cardStyle);
		bg.AddChild(centerCard);

		if (ResourceLoader.Exists("res://sprites/L8.png"))
		{
			var sp = new Sprite2D
			{
				Texture = GD.Load<Texture2D>("res://sprites/L8.png"),
				Position = new Vector2(290, 230),
				Scale = new Vector2(0.85f, 0.85f)
			};
			centerCard.AddChild(sp);
		}

		var adTitle = new Label
		{
			Text = "RoboMerge",
			Position = new Vector2(20, 390),
			Size = new Vector2(540, 70),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(adTitle, 52, new Color(1.0f, 0.78f, 0.20f), new Color(0.20f, 0.10f, 0.04f), 12);
		centerCard.AddChild(adTitle);

		var badgeLbl = new Label
		{
			Text = "Google AdMob / App Store Test Ad",
			Position = new Vector2(20, 470),
			Size = new Vector2(540, 40),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(badgeLbl, 22, new Color(0.65f, 0.85f, 1.0f), new Color(0.08f, 0.14f, 0.22f), 6);
		centerCard.AddChild(badgeLbl);

		_progressBar = new ProgressBar
		{
			Position = new Vector2(60, 560),
			Size = new Vector2(460, 32),
			MinValue = 0,
			MaxValue = 3.0,
			Value = 0,
			ShowPercentage = false
		};
		centerCard.AddChild(_progressBar);
	}

	private void StartInteractiveRewardedAd()
	{
		_mockAdRemainingSec = 3.0f;
		_mockAdActive = true;
		_closeAdButton.Visible = false;
		_progressBar.Value = 0;
		_mockAdLayer.Visible = true;
		UpdateCountdownText();
	}

	public override void _Process(double delta)
	{
		if (!_mockAdActive || Engine.IsEditorHint()) return;

		_mockAdRemainingSec -= (float)delta;
		_progressBar.Value = Mathf.Clamp(3.0f - _mockAdRemainingSec, 0f, 3.0f);

		if (_mockAdRemainingSec <= 0f)
		{
			_mockAdRemainingSec = 0f;
			_isRewardEarned = true;
			_closeAdButton.Visible = true;
		}

		UpdateCountdownText();
	}

	private void UpdateCountdownText()
	{
		bool en = GameData.Language == "EN";
		if (_isRewardEarned)
		{
			_countdownLabel.Text = en ? "✓ Reward Granted! Close to continue" : "✓ Ödül Kazanıldı! Devam için kapatın";
		}
		else
		{
			int sec = Mathf.CeilToInt(_mockAdRemainingSec);
			_countdownLabel.Text = en ? $"Reward in {sec}s..." : $"Ödüle {sec} sn kaldı...";
		}
	}

	private void OnCloseRewardedAdPressed()
	{
		_mockAdActive = false;
		_mockAdLayer.Visible = false;

		// Ses durumunu eski haline döndür
		AudioServer.SetBusMute(0, false);

		if (_isRewardEarned)
		{
			_onRewardCallback?.Invoke();
		}
		else
		{
			_onClosedWithoutRewardCallback?.Invoke();
		}
	}
}
