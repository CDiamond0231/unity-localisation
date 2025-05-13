using System;
using UnityEngine;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Localisation.Localisation.Editor
{
    public class IosRegion
    {
#if UNITY_IOS
	    [PostProcessBuild]
	    static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	    {
		    try
		    {
			    string filePath = Path.Combine(pathToBuiltProject, "Classes/UnityAppController.mm");
			    string content = File.ReadAllText(filePath);
			    content = content.Replace(@"::printf(""-> applicationDidFinishLaunching()\n"");", @"::printf(""-> applicationDidFinishLaunching()\n"");

	    NSLocale *locale = [NSLocale currentLocale];
	    NSString *country = [locale objectForKey:NSLocaleCountryCode];
	    printf_console(""\n\n\nDevice Region:[%s]\n\n\n"", [country UTF8String]);
	    [[NSUserDefaults standardUserDefaults] setObject:country forKey:@""Region""];
	    [[NSUserDefaults standardUserDefaults] synchronize];");

			    File.WriteAllText(filePath, content);

			    LocalisationOverrides.Debug.LogInfo("iOS region code inserted to Xcode project.\nContent:\n\n" + content);
		    }
		    catch (Exception e)
		    {
			    Debug.LogError(e.Message);
		    }
	    }
#endif
    }
}