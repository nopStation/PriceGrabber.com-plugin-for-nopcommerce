using Nop.Core;
using Nop.Services.Plugins;
using Nop.Services.Common;
using Nop.Services.Localization;
using System.Threading.Tasks;

namespace Nop.Plugin.Feed.PriceGrabber
{
    public class PriceGrabberService : BasePlugin,  IMiscPlugin
    {
        #region Fields
       
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Ctor

        public PriceGrabberService(IWebHelper webHelper,
            ILocalizationService localizationService)
        {
            this._webHelper = webHelper;
            this._localizationService = localizationService;
        }

        #endregion

        #region Methods

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/FeedPriceGrabber/Configure";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override async Task InstallAsync()
        {
            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.ClickHere", "Click here to see generated feed");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.Currency", "Currency");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.Currency.Hint", "Select the currency that will be used to generate the feed.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.ProductPictureSize", "Product thumbnail image size");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.ProductPictureSize.Hint", "The size in pixels for product thumbnail images (longest size).");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.Generate", "Generate feed");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.PriceGrabber.Success", "PriceGrabber feed has been successfully generated");

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override async Task UninstallAsync()
        {
            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.ClickHere");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.Currency");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.Currency.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.ProductPictureSize");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.Fields.ProductPictureSize.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.Generate");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.PriceGrabber.Success");

            await base.UninstallAsync();
        }

        #endregion
    }
}
