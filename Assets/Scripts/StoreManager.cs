using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

public class StoreManager : MonoBehaviour, IStoreListener
{
    // Setup variables
    private static IStoreController _storeController;
    private static IExtensionProvider _extensionProvider;

    // Product IDs
    public const string productIDConsumable_MediumPotion = "health_potion_medium";
    public const string productIDNonConsumable_RareHelm = "gold_helm_rare";
    public const string productIDSubscriptoin_AutoRenew = "monthly_access_auto_renew";

    // Computed properties
    public bool isInitialized
    {
        get { return _storeController != null && _extensionProvider != null; }
    }

    // Start is called before the first frame update
    void Start()
    {
        InitializeIAP();
    }

    #region Public IAP methods -> UI accessible
    public void BuyConsumableItem()
    {
        PurchaseItem(productIDConsumable_MediumPotion);
    }

    public void BuyNonConsumableItem()
    {
        PurchaseItem(productIDNonConsumable_RareHelm);
    }

    public void RestorePurchases()
    {
        RestorePurchasedItems();
    }

    public void BuySubscription()
    {
        PurchaseItem(productIDSubscriptoin_AutoRenew);
    }
    #endregion

    #region Private IAP methods
    private void InitializeIAP()
    {
        if (isInitialized)
            return;

        var purchasingModule = StandardPurchasingModule.Instance();
        purchasingModule.useFakeStoreUIMode = FakeStoreUIMode.DeveloperUser;

        var builder = ConfigurationBuilder.Instance(purchasingModule);
        builder.AddProduct(productIDConsumable_MediumPotion, ProductType.Consumable);
        builder.AddProduct(productIDNonConsumable_RareHelm, ProductType.NonConsumable, null, new PayoutDefinition(PayoutType.Item, "Custom subtype", 1, "Data"));
        builder.AddProduct(productIDSubscriptoin_AutoRenew, ProductType.Subscription, new IDs
        {
            {"com.CompanyName.GameTitle.subscription.auto", MacAppStore.Name},
            {"com.CompanyName.GameTitle.subscription.automatic", GooglePlay.Name}
        });

        UnityPurchasing.Initialize(this, builder);
    }

    private void PurchaseItem(string productID)
    {
        if(!isInitialized)
        {
            Debug.Log("Product purchase failed, IAP not initialized...");
            return;
        }

        Product currentProduct = _storeController.products.WithID(productID);
        if(currentProduct != null && currentProduct.availableToPurchase)
        {
            Debug.LogFormat("Attempting to purchase item {0} asynchronously...", productID);
            _storeController.InitiatePurchase(currentProduct);
        }
        else
        {
            Debug.LogFormat("Attempt to purchase item {0} failed - item was not found or unavailable...", productID);
        }
    }

    private void RestorePurchasedItems()
    {
        if (!isInitialized)
            return;

        if(Application.platform == RuntimePlatform.IPhonePlayer || 
            Application.platform == RuntimePlatform.OSXPlayer || 
            Application.platform == RuntimePlatform.tvOS)
        {
            IAppleExtensions appleExtensions = _extensionProvider.GetExtension<IAppleExtensions>();
            appleExtensions.RestoreTransactions((restoreResult) =>
            {
                Debug.LogFormat("Purchase restoration processing: {0}. If ProcessPurchase doesn't fire there are no products to restore", restoreResult);
            });
        }
        else if (Application.platform == RuntimePlatform.Android || 
                 Application.platform == RuntimePlatform.WindowsPlayer)
        {
            Debug.Log("Purchases have been restored automatically, and ProcessPurchase will fire for every restored item found!");
        }
        else
        {
            Debug.LogFormat("Purchase restoration not supported on {0} platform", Application.platform);
        }
    }

    private void QuerySubscriptionInfo(Product product)
    {
        Dictionary<string, string> price_json = _extensionProvider.GetExtension<IAppleExtensions>().GetIntroductoryPriceDictionary();

        if(product.receipt != null)
        {
            Debug.Log(product.receipt);
            if(product.definition.type == ProductType.Subscription)
            {
                string info_json = (price_json == null || !price_json.ContainsKey(product.definition.storeSpecificId)) ? null : price_json[product.definition.storeSpecificId];
                SubscriptionManager subManager = new SubscriptionManager(product, info_json);
                SubscriptionInfo info = subManager.getSubscriptionInfo();

                Debug.LogFormat("{0},{1}, {2}", info.getPurchaseDate(), info.getExpireDate(), info.isAutoRenewing());
            }
            else
            {
                Debug.Log("This product is not a subscription...");
            }
        }
        else
        {
            Debug.Log("This product does not have a valid receipt...");
        }
    }

    private bool ValidateReceipt(Product product)
    {
        bool validRecipt = true;

#if UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX
        var validator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);

        try
        {
            var result = validator.Validate(product.receipt);
            foreach(IPurchaseReceipt productReceipt in result)
            {
                Debug.LogFormat("{0}, {1}, {2}", productReceipt.productID, productReceipt.purchaseDate, productReceipt.transactionID);

                AppleInAppPurchaseReceipt appleReceipt = productReceipt as AppleInAppPurchaseReceipt;
                if(appleReceipt != null)
                {
                    Debug.LogFormat("{0}", appleReceipt.originalTransactionIdentifier);
                }

                GooglePlayReceipt googleReceipt = productReceipt as GooglePlayReceipt;
                if(googleReceipt != null)
                {
                    Debug.LogFormat("{0}", googleReceipt.transactionID);
                }
            }
        }
        catch (MissingStoreSecretException e)
        {
            Debug.Log("You haven't supplied a secret key...");
            Debug.LogException(e);
            validRecipt = false;
        } 
        catch (IAPSecurityException e)
        {
            Debug.LogFormat("Invalid receipt {0}", product.receipt);
            Debug.LogException(e);
            validRecipt = false;
        }
#endif

        return validRecipt;
    }
    #endregion

    #region IStoreListner methods
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _storeController = controller;
        _extensionProvider = extensions;
        
        _extensionProvider.GetExtension<IAppleExtensions>().RegisterPurchaseDeferredListener((item) =>
        {
            Debug.LogFormat("Transaction defferred for {0}", item.definition.id);
        });

        Debug.Log("IAP initialized!");

        foreach(Product product in _storeController.products.all)
        {
            Debug.LogFormat("{0}, {1}, {2}", product.metadata.localizedTitle, 
                                             product.metadata.localizedDescription,
                                             product.metadata.localizedPriceString);
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log("Purchasing failed to initialize...");
        switch(error)
        {
            case InitializationFailureReason.AppNotKnown:
                Debug.Log("Check that your app is correctly configured on you publishing platfomr...");
                break;
            case InitializationFailureReason.NoProductsAvailable:
                Debug.Log("No products are available for purchase...");
                break;
            case InitializationFailureReason.PurchasingUnavailable:
                Debug.Log("Purchasing service is unavailable...");
                break;
        }
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productID = args.purchasedProduct.definition.id;

        PayoutDefinition payout = args.purchasedProduct.definition.payout;
        if (payout != null)
            Debug.Log("Payout for this item detected...");

        bool validReceipt = ValidateReceipt(args.purchasedProduct);
            
        switch(productID)
        {
            case productIDConsumable_MediumPotion:
                Debug.LogFormat("Consumable product {0} successfully purchased!", productID);
                break;
            case productIDNonConsumable_RareHelm:
                Debug.LogFormat("Non-Consumable product {0} successfully purchased!", productID);
                break;
            case productIDSubscriptoin_AutoRenew:
                Debug.LogFormat("Subscription {0} successfully purchased!", productID);
                QuerySubscriptionInfo(args.purchasedProduct);
                break;
            default:
                Debug.LogFormat("Product ID {0} not recognized...", productID);
                break;
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failure)
    {
        Debug.LogFormat("Product {0} purchase failed...", product.definition.storeSpecificId);
        switch(failure)
        {
            case PurchaseFailureReason.PaymentDeclined:
                Debug.Log("Your payment was declined for some reason...");
                break;
            case PurchaseFailureReason.ProductUnavailable:
                Debug.Log("That product is no longer available for purchase...");
                break;
            case PurchaseFailureReason.Unknown:
                Debug.Log("An unknown problem occurred with your purchase...");
                break;
        }
    }
    #endregion

}
