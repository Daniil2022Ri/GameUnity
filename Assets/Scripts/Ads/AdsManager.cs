using System;
using UI.MessageUI;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace Ads
{
    /// <summary>
    /// Single entry point for Yandex Mobile Ads in the game. The SDK is installed as the UPM
    /// package <c>com.yandex.mobileads</c> (8.6.0) from the OpenUPM registry, which pulls the
    /// Google Resolver (EDM4U) and the AppMetrica ad-revenue adapter as dependencies - see
    /// <c>Packages/manifest.json</c>. No SDK files are stored in Assets.
    ///
    /// Wraps interstitial, rewarded and sticky banner ads, hides the SDK's single-use ad
    /// lifecycle (load -> show -> destroy -> reload) and reports failures to the UI through
    /// <see cref="MessageManager"/>.
    ///
    /// In the Unity editor the SDK swaps in dummy clients, so the whole flow (load, show,
    /// callbacks) can be exercised without a device, but no real ads are displayed.
    ///
    /// Usage:
    /// <code>
    /// AdsManager.Instance.ShowRewarded();          // plays if preloaded
    /// AdsManager.Instance.RewardedCompleted += reward => GiveCoins(reward.amount);
    /// </code>
    ///
    /// Note: replace the demo ad unit IDs with your own before shipping.
    /// </summary>
    [DisallowMultipleComponent]
    public class AdsManager : MonoBehaviour
    {
        /// <summary>Manager of the current scene; null when there is none.</summary>
        public static AdsManager Instance { get; private set; }

        [Header("Ad Unit IDs (demo IDs work in test builds)")]
        [SerializeField] private string interstitialAdUnitId = "demo-interstitial-yandex";
        [SerializeField] private string rewardedAdUnitId = "demo-rewarded-yandex";
        [SerializeField] private string bannerAdUnitId = "demo-banner-yandex";

        [Header("SDK settings")]
        [Tooltip("Preloads interstitial and rewarded ads on Start so they are ready to show.")]
        [SerializeField] private bool preloadOnStart = true;
        [Tooltip("GDPR consent for collecting personal data used for ad targeting.")]
        [SerializeField] private bool userConsent = true;
        [Tooltip("Marks the user as age-restricted: personal data is never collected.")]
        [SerializeField] private bool ageRestricted;
        [Tooltip("Allows the SDK to use the device location for ad targeting.")]
        [SerializeField] private bool locationTracking;

        [Header("Banner (optional)")]
        [SerializeField] private bool showBannerOnStart;
        [SerializeField] private AdPosition bannerPosition = AdPosition.BottomCenter;
        [Tooltip("Maximum banner width in density-independent pixels.")]
        [SerializeField, Min(1)] private int bannerWidthDp = 320;

        [Header("Behaviour")]
        [Tooltip("Loads a fresh ad after every show/dismiss, because a single ad can be shown only once.")]
        [SerializeField] private bool autoReloadAfterShow = true;

        [Header("User messages (optional)")]
        [Tooltip("Shows a UI message from MessageManager when an ad cannot be loaded or shown.")]
        [SerializeField] private bool showMessages = true;
        [SerializeField] private string unavailableMessage = "Реклама недоступна";

        private InterstitialAdLoader _interstitialLoader;
        private RewardedAdLoader _rewardedLoader;
        private Interstitial _interstitial;
        private RewardedAd _rewarded;
        private Banner _banner;
        private bool _interstitialLoading;
        private bool _rewardedLoading;

        /// <summary>Interstitial is loaded and may be shown right now.</summary>
        public bool IsInterstitialReady => _interstitial != null;

        /// <summary>Rewarded ad is loaded and may be shown right now.</summary>
        public bool IsRewardedReady => _rewarded != null;

        /// <summary>True while an interstitial is being fetched from the network.</summary>
        public bool IsInterstitialLoading => _interstitialLoading;

        /// <summary>True while a rewarded ad is being fetched from the network.</summary>
        public bool IsRewardedLoading => _rewardedLoading;

        public event Action InterstitialLoaded;
        public event Action InterstitialClosed;
        public event Action RewardedLoaded;
        public event Action RewardedClosed;

        /// <summary>Fired when a rewarded ad pays out. <see cref="Reward.amount"/> / <see cref="Reward.type"/>.</summary>
        public event Action<Reward> RewardedCompleted;

        /// <summary>Fired with the SDK error text whenever an ad cannot be loaded or shown.</summary>
        public event Action<string> AdFailed;

        private void Awake()
        {
            Instance = this;

            // Global flags must be set before the first ad request is created.
            YandexAds.SetUserConsent(userConsent);
            YandexAds.SetAgeRestricted(ageRestricted);
            YandexAds.SetLocationTracking(locationTracking);
        }

        private void Start()
        {
            if (preloadOnStart)
            {
                LoadInterstitial();
                LoadRewarded();
            }

            if (showBannerOnStart)
                ShowBanner();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            DestroyBanner();
            DestroyInterstitial();
            DestroyRewarded();

            _interstitialLoader?.CancelLoading();
            _rewardedLoader?.CancelLoading();
        }

        // -----------------------------------------------------------------
        // Interstitial (full-screen, no reward)
        // -----------------------------------------------------------------

        /// <summary>Starts loading an interstitial. Safe to call when one is already loaded or loading.</summary>
        public void LoadInterstitial()
        {
            if (_interstitial != null || _interstitialLoading)
                return;

            _interstitialLoading = true;

            if (_interstitialLoader == null)
                _interstitialLoader = new InterstitialAdLoader();

            _interstitialLoader.LoadAd(
                new AdRequest(interstitialAdUnitId),
                HandleInterstitialLoaded,
                error =>
                {
                    _interstitialLoading = false;
                    ReportFailure($"interstitial load failed: {error.Message}");
                });
        }

        /// <summary>
        /// Shows the loaded interstitial. The ad is single-use, so a new one is loaded
        /// afterwards when <see cref="autoReloadAfterShow"/> is on.
        /// </summary>
        public void ShowInterstitial()
        {
            if (_interstitial == null)
            {
                ReportFailure("interstitial is not loaded yet");
                LoadInterstitial();
                return;
            }

            _interstitial.Show();
        }

        private void HandleInterstitialLoaded(Interstitial ad)
        {
            _interstitialLoading = false;
            _interstitial = ad;

            ad.OnAdDismissed += HandleInterstitialDismissed;
            ad.OnAdFailedToShow += HandleInterstitialFailedToShow;

            InterstitialLoaded?.Invoke();
        }

        private void HandleInterstitialDismissed(object sender, EventArgs args)
        {
            DestroyInterstitial();
            InterstitialClosed?.Invoke();

            if (autoReloadAfterShow)
                LoadInterstitial();
        }

        private void HandleInterstitialFailedToShow(object sender, AdFailureEventArgs args)
        {
            ReportFailure($"interstitial show failed: {args.Message}");
            DestroyInterstitial();

            if (autoReloadAfterShow)
                LoadInterstitial();
        }

        // -----------------------------------------------------------------
        // Rewarded (full-screen, pays out on completion)
        // -----------------------------------------------------------------

        /// <summary>Starts loading a rewarded ad. Safe to call when one is already loaded or loading.</summary>
        public void LoadRewarded()
        {
            if (_rewarded != null || _rewardedLoading)
                return;

            _rewardedLoading = true;

            if (_rewardedLoader == null)
                _rewardedLoader = new RewardedAdLoader();

            _rewardedLoader.LoadAd(
                new AdRequest(rewardedAdUnitId),
                HandleRewardedLoaded,
                error =>
                {
                    _rewardedLoading = false;
                    ReportFailure($"rewarded load failed: {error.Message}");
                });
        }

        /// <summary>
        /// Shows the loaded rewarded ad. Subscribe to <see cref="RewardedCompleted"/> to grant
        /// the reward - it fires only when the user has watched the ad to the end.
        /// </summary>
        public void ShowRewarded()
        {
            if (_rewarded == null)
            {
                ReportFailure("rewarded ad is not loaded yet");
                LoadRewarded();
                return;
            }

            _rewarded.Show();
        }

        private void HandleRewardedLoaded(RewardedAd ad)
        {
            _rewardedLoading = false;
            _rewarded = ad;

            ad.OnRewarded += HandleRewarded;
            ad.OnAdDismissed += HandleRewardedDismissed;
            ad.OnAdFailedToShow += HandleRewardedFailedToShow;

            RewardedLoaded?.Invoke();
        }

        private void HandleRewarded(object sender, Reward reward)
        {
            RewardedCompleted?.Invoke(reward);
        }

        private void HandleRewardedDismissed(object sender, EventArgs args)
        {
            DestroyRewarded();
            RewardedClosed?.Invoke();

            if (autoReloadAfterShow)
                LoadRewarded();
        }

        private void HandleRewardedFailedToShow(object sender, AdFailureEventArgs args)
        {
            ReportFailure($"rewarded show failed: {args.Message}");
            DestroyRewarded();

            if (autoReloadAfterShow)
                LoadRewarded();
        }

        // -----------------------------------------------------------------
        // Banner (sticky, stays on screen)
        // -----------------------------------------------------------------

        /// <summary>Creates and loads the sticky banner. It becomes visible as soon as it loads.</summary>
        public void ShowBanner()
        {
            if (_banner != null)
            {
                _banner.Show();
                return;
            }

            _banner = new Banner(BannerAdSize.Sticky(bannerWidthDp), bannerPosition);
            _banner.OnAdLoaded += HandleBannerLoaded;
            _banner.OnAdFailedToLoad += HandleBannerFailed;
            _banner.LoadAd(new AdRequest(bannerAdUnitId));
        }

        /// <summary>Hides the banner without destroying it.</summary>
        public void HideBanner()
        {
            _banner?.Hide();
        }

        private void HandleBannerLoaded(object sender, EventArgs args)
        {
            _banner?.Show();
        }

        private void HandleBannerFailed(object sender, AdFailureEventArgs args)
        {
            ReportFailure($"banner load failed: {args.Message}");
        }

        private void DestroyBanner()
        {
            if (_banner == null)
                return;

            _banner.OnAdLoaded -= HandleBannerLoaded;
            _banner.OnAdFailedToLoad -= HandleBannerFailed;
            _banner.Destroy();
            _banner = null;
        }

        // -----------------------------------------------------------------
        // Cleanup / helpers
        // -----------------------------------------------------------------

        private void DestroyInterstitial()
        {
            if (_interstitial == null)
                return;

            _interstitial.OnAdDismissed -= HandleInterstitialDismissed;
            _interstitial.OnAdFailedToShow -= HandleInterstitialFailedToShow;
            _interstitial.Destroy();
            _interstitial = null;
        }

        private void DestroyRewarded()
        {
            if (_rewarded == null)
                return;

            _rewarded.OnRewarded -= HandleRewarded;
            _rewarded.OnAdDismissed -= HandleRewardedDismissed;
            _rewarded.OnAdFailedToShow -= HandleRewardedFailedToShow;
            _rewarded.Destroy();
            _rewarded = null;
        }

        /// <summary>Opens the SDK debug panel (Android only, debug builds).</summary>
        public void ShowDebugPanel()
        {
            YandexAds.ShowDebugPanel();
        }

        /// <summary>Logs the SDK error, notifies listeners and shows the UI message.</summary>
        private void ReportFailure(string details)
        {
            Debug.LogWarning($"{nameof(AdsManager)}: {details}");
            AdFailed?.Invoke(details);

            if (showMessages)
                MessageManager.TryShow(unavailableMessage, MessageType.Warning);
        }
    }
}
