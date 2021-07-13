using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Feed.PriceGrabber.Models;
using Nop.Services;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Feed.PriceGrabber.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class FeedPriceGrabberController : BasePluginController
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICategoryService _categoryService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IManufacturerService _manufacturerService;
        private readonly IPictureService _pictureService;
        private readonly IProductService _productService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IPermissionService _permissionService;
        private readonly IUrlRecordService _urlRecordService;
        private readonly INotificationService _notificationService;

        #endregion

        #region Ctor

        public FeedPriceGrabberController(CurrencySettings currencySettings,
            ICategoryService categoryService,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            ILogger logger,
            IManufacturerService manufacturerService,
            IPictureService pictureService,
            IProductService productService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWebHostEnvironment webHostEnvironment,
            IPermissionService permissionService,
            IUrlRecordService urlRecordService,
            INotificationService notificationService)
        {
            _currencySettings = currencySettings;
            _categoryService = categoryService;
            _currencyService = currencyService;
            _localizationService = localizationService;
            _logger = logger;
            _manufacturerService = manufacturerService;
            _pictureService = pictureService;
            _productService = productService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _webHostEnvironment = webHostEnvironment;
            _permissionService = permissionService;
            _urlRecordService = urlRecordService;
            _notificationService = notificationService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Replace some special characters
        /// </summary>
        /// <param name="inputString">Input string</param>
        /// <returns>Output string</returns>
        protected string ReplaceSpecChars(string inputString)
        {
            if (string.IsNullOrEmpty(inputString))
                return inputString;

            return inputString.Replace(';', ',').Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        #endregion

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            var model = new FeedPriceGrabberModel
            {
                ProductPictureSize = 125,
                CurrencyId = _currencySettings.PrimaryStoreCurrencyId,
                AvailableCurrencies = (await _currencyService.GetAllCurrenciesAsync()).ToSelectList(x => ((Currency) x).Name)
            };
            
            return View("~/Plugins/Feed.PriceGrabber/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("generate")]
        public async Task<IActionResult> GenerateFeed(FeedPriceGrabberModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var storeUrl = _webHelper.GetStoreLocation();

            try
            {
                var fileName = $"priceGrabber_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}_{CommonHelper.GenerateRandomDigitCode(4)}.csv";
                using (var writer = new StreamWriter(Path.Combine(_webHostEnvironment.WebRootPath, "files\\exportimport", fileName)))
                {
                    //write header
                    writer.WriteLine("Unique Retailer SKU;Manufacturer Name;Manufacturer Part Number;Product Title;Categorization;" +
                        "Product URL;Image URL;Detailed Description;Selling Price;Condition;Availability");

                    foreach (var parentProduct in await _productService.SearchProductsAsync(storeId: storeScope, visibleIndividuallyOnly: true))
                    {
                        //get single products from all of these
                        var singleProducts = new List<Product>();
                        switch (parentProduct.ProductType)
                        {
                            case ProductType.SimpleProduct:
                                {
                                    //simple product doesn't have child products
                                    singleProducts.Add(parentProduct);
                                }
                                break;
                            case ProductType.GroupedProduct:
                                {
                                    //grouped products could have several child products
                                    singleProducts.AddRange(await _productService.GetAssociatedProductsAsync(parentProduct.Id, storeScope));
                                }
                                break;
                            default:
                                continue;
                        }

                        //get line for the each product
                        foreach (var product in singleProducts)
                        {
                            //sku
                            var sku = !string.IsNullOrEmpty(product.Sku) ? product.Sku : product.Id.ToString();
                            sku = ReplaceSpecChars(sku);

                            //manufacturer name
                            var productManufacturer = (await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id)).FirstOrDefault();
                            var manufacturerName = string.Empty;

                            if (productManufacturer != null)
                            {
                                var manufacturer = await _manufacturerService.GetManufacturerByIdAsync(productManufacturer.ManufacturerId);
                                manufacturerName = manufacturer?.Name ?? string.Empty;
                                manufacturerName = ReplaceSpecChars(manufacturerName);
                            }

                            //manufacturer part number
                            var manufacturerPartNumber = ReplaceSpecChars(product.ManufacturerPartNumber);

                            //product title
                            var productTitle = ReplaceSpecChars(product.Name);

                            //category
                            var productCategory = (await _categoryService.GetProductCategoriesByProductIdAsync(product.Id)).FirstOrDefault();
                            var categorization = string.Empty;

                            if (productCategory != null)
                            {
                                var category = await _categoryService.GetCategoryByIdAsync(productCategory.CategoryId);
                                categorization = category != null
                                    ? await _categoryService.GetFormattedBreadCrumbAsync(category, separator: ">") : "no category";
                                categorization = ReplaceSpecChars(categorization);
                            }

                            //product URL
                            var productUrl = $"{storeUrl}{_urlRecordService.GetSeNameAsync(product)}";

                            //image Url
                            var picture = (await _pictureService.GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();
                            var storeUrlNotSsl = _webHelper.GetStoreLocation(false); //always use HTTP when getting image URL
                            var imageUrl = picture != null
                                ? (await _pictureService.GetPictureUrlAsync(picture, model.ProductPictureSize, storeLocation: storeUrlNotSsl)).Url
                                : await _pictureService.GetDefaultPictureUrlAsync(model.ProductPictureSize, storeLocation: storeUrlNotSsl);

                            //description
                            var description = !string.IsNullOrEmpty(product.FullDescription) ? product.FullDescription
                                : !string.IsNullOrEmpty(product.ShortDescription) ? product.ShortDescription : product.Name;
                            description = ReplaceSpecChars(Core.Html.HtmlHelper.StripTags(description));

                            //price
                            var currency = await _currencyService.GetCurrencyByIdAsync(model.CurrencyId);
                            var priceAmount = currency != null ? await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(product.Price, currency) : product.Price;
                            var price = priceAmount.ToString(new CultureInfo("en-US", false).NumberFormat);

                            //condition
                            var condition = "New";

                            //availability
                            var availability = product.StockQuantity > 0 ? "Yes" : "No";

                            //write line
                            writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}",
                                sku, manufacturerName, manufacturerPartNumber, productTitle, categorization,
                                productUrl, imageUrl, description, price, condition, availability);
                        }
                    }
                }

                //link for the result
                model.GenerateFeedResult = $"<a href=\"{storeUrl}files/exportimport/{fileName}\" target=\"_blank\">{await _localizationService.GetResourceAsync("Plugins.Feed.PriceGrabber.ClickHere")}</a>";

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Feed.PriceGrabber.Success"));
            }
            catch (Exception exc)
            {
                _notificationService.ErrorNotification(exc.Message);
                await _logger.ErrorAsync(exc.Message, exc);
            }

            //prepare currencies
            model.AvailableCurrencies = (await _currencyService.GetAllCurrenciesAsync()).ToSelectList(x => ((Currency) x).Name);

            return View("~/Plugins/Feed.PriceGrabber/Views/Configure.cshtml", model);
        }

        #endregion
    }
}
