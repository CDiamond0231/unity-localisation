//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Post Processor
//             Author: Christopher Allport
//             Date Created: 27th May, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Post Processor for Localisation. Will move Resources
//      back to the usual locations if the Pre Processor moved
//      them into resources.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using UnityEngine;




namespace Localisation.Localisation.Editor
{
	public class LocalisationPostProcessor : UnityEditor.Build.IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;
		public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport _report)
		{
			//ExecutePostProcessor();
		}

		private static void ExecutePostProcessor()
		{
			LocalisationResourcesBuildHandler.MoveLocResourcesBackToAssets();
		}
	}
}