﻿using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using VirtoCommerce.Storefront.Common;
using VirtoCommerce.Storefront.Model;
using VirtoCommerce.Storefront.Model.Cart.Services;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.Services;


namespace VirtoCommerce.Storefront.Controllers.Api
{
    [HandleJsonError]
    public class ApiListsController : StorefrontControllerBase
    {
        private readonly ICartBuilder _wishlistBuilder;
        private readonly ICatalogSearchService _catalogSearchService;

        public ApiListsController(WorkContext workContext, ICatalogSearchService catalogSearchService, ICartBuilder cartBuilder,
                                     IStorefrontUrlBuilder urlBuilder)
            : base(workContext, urlBuilder)
        {
            _wishlistBuilder = cartBuilder;
            _catalogSearchService = catalogSearchService;
        }

        // GET: storefrontapi/lists
        [HttpGet]
        public async Task<ActionResult> GetListByName(string listName)
        {
            var wishlistBuilder = await LoadOrCreateWishlistAsync(listName);
            await wishlistBuilder.ValidateAsync();
            return Json(wishlistBuilder.Cart, JsonRequestBehavior.AllowGet);
        }

        // GET: storefrontapi/lists/contains
        [HttpGet]
        public async Task<ActionResult> IsItemContainsInList(string productId, string listName)
        {
            var wishlistBuilder = await LoadOrCreateWishlistAsync(listName);
            await wishlistBuilder.ValidateAsync();
            var hasProduct = wishlistBuilder.Cart.Items.Any(x => x.ProductId == productId);
            return Json(new { Contains = hasProduct }, JsonRequestBehavior.AllowGet);
        }

        // POST: storefrontapi/lists/items?id=...
        [HttpPost]
        public async Task<ActionResult> AddItemToList(string productId, string listName)
        {
            //Need lock to prevent concurrent access to same cart
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(WorkContext, listName)).LockAsync())
            {
                var wishlistBuilder = await LoadOrCreateWishlistAsync(listName);

                var products = await _catalogSearchService.GetProductsAsync(new[] { productId }, Model.Catalog.ItemResponseGroup.ItemLarge);
                if (products != null && products.Any())
                {
                    await wishlistBuilder.AddItemAsync(products.First(), 1);
                    await wishlistBuilder.SaveAsync();
                }
                return Json(new { ItemsCount = wishlistBuilder.Cart.ItemsQuantity });
            }
        }

        // DELETE: storefrontapi/lists/items?id=...
        [HttpDelete]
        public async Task<ActionResult> RemoveItemFromList(string lineItemId, string listName)
        {
            //Need lock to prevent concurrent access to same cart
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(WorkContext, listName)).LockAsync())
            {
                var wishlistBuilder = await LoadOrCreateWishlistAsync(listName);
                await wishlistBuilder.RemoveItemAsync(lineItemId);
                await wishlistBuilder.SaveAsync();
                return Json(new { ItemsCount = wishlistBuilder.Cart.ItemsQuantity });
            }

        }
        private static string GetAsyncLockCartKey(WorkContext context, string listName)
        {
            return string.Join(":", listName, context.CurrentCustomer.Id, context.CurrentStore.Id);
        }

        private async Task<ICartBuilder> LoadOrCreateWishlistAsync(string listName)
        {
            await _wishlistBuilder.LoadOrCreateNewTransientCartAsync(listName, WorkContext.CurrentStore, WorkContext.CurrentCustomer, WorkContext.CurrentLanguage, WorkContext.CurrentCurrency);
            return _wishlistBuilder;
        }
    }
}
