using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CodelessPurchases : MonoBehaviour
{
    public void UnlockBoostProduct()
    {
        Debug.Log("Boost product was purchased!");
    }

    public void BoostPurchaseFailed()
    {
        Debug.Log("Boost purchase failed...");
    }
}
