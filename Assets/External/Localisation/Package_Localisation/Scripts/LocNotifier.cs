//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Notifier
//             Author: Christopher Allport
//             Date Created: 14th June, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Changes Loc Languages
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using UnityEngine;
using System.Collections;
using System;

#if LOCALISATION_ADDRESSABLES
using System.Threading.Tasks;
#endif


namespace Localisation.Localisation
{
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //             Localisation Notifier - Addressables Mode
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
    public class LocNotifier : MonoBehaviour
    {
        public async Task ChangeToNextLanguage() 
        { 
            await ChangeLanguage(true); 
        }
        
        public async Task ChangeToPreviousLanguage() 
        { 
            await ChangeLanguage(false); 
        }

        /// <summary> Changes to the next/previous language </summary>
        /// <param name="increase"> True changes to the next language, false changes to the previous language </param>
        public async Task ChangeLanguage(bool increase)
        {
            // Move to next/previous
            int language = (int)LocManager.CurrentLanguage + (increase ? 1 : -1);
            await ChangeLanguage(language);
        }

        /// <summary> Changes to the specified language </summary>
        /// <param name="a_language"> New language to change to </param>
        public async Task ChangeLanguage(int a_language)
        {
            // Wrap around if necessary
            int numLanguages = Enum.GetValues(typeof(LocLanguages)).Length;
            if (a_language >= numLanguages)
                a_language = 0;
            else if (a_language < 0)
                a_language = numLanguages - 1;

            await LocManager.SetLanguage((LocLanguages)a_language);
        }
    }

    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //             Localisation Notifier - Resources Mode
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#else
    public class LocNotifier : MonoBehaviour
    {
        public void ChangeToNextLanguage() { ChangeLanguage(true); }
        public void ChangeToPreviousLanguage() { ChangeLanguage(false); }

        /// <summary> Changes to the next/previous language </summary>
        /// <param name="increase"> True changes to the next language, false changes to the previous language </param>
        public void ChangeLanguage(bool increase)
        {
            // Move to next/previous
            int language = (int)(LocManager.CurrentLanguage) + (increase ? 1 : -1);
            ChangeLanguage(language);
        }

        /// <summary> Changes to the specified language </summary>
        /// <param name="a_language"> New language to change to </param>
        public void ChangeLanguage(int a_language)
        {
            // Wrap around if necessary
            int numLanguages = Enum.GetValues(typeof(LocLanguages)).Length;
            if (a_language >= numLanguages)
                a_language = 0;
            else if (a_language < 0)
                a_language = numLanguages - 1;

            LocManager.SetLanguage((LocLanguages)a_language);
        }
    }
#endif
}
