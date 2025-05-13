//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Pre Processor
//             Author: Christopher Allport
//             Date Created: 27th May, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      PreProcessor for the Localisation system. Will move Localisation Assets
//      into the resources folder prior to build. The PostProcessor will then
//      move them back to where they should be.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using UnityEngine;




namespace Localisation.Localisation.Editor
{
	public class LocalisationPreProcessor : UnityEditor.Build.IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;
		public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport _report)
		{
			ExecutePreProcessor();
		}

		private static void ExecutePreProcessor()
		{
			LocalisationResourcesBuildHandler.AssignLocAssetsToResourcesAndAddressables();
		}
	}
}