//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             ILocalisation Service
//             Author: Simon Pederick
//             Date Created: 15th December, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Interface for the localisation service.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Localisation.GameSpecificLocalisation.Services
{
    /// <summary> Interface for the localisation service. </summary>
    public interface ILocalisationService
    {
        /// <summary> Returns if localisation has fully initialised. </summary>
        bool LocalisationInitialised { get; }
    }
}
