using System;
using System.Diagnostics;
using System.Reflection;
using Terminal.Gui;
using Xunit;

// Since Application is a singleton we can't run tests in parallel
[assembly: CollectionBehavior (DisableTestParallelization = true)]

// This class enables test functions annotated with the [AutoInitShutdown] attribute to 
// automatically call Application.Init before called and Application.Shutdown after
// 
// This is necessary because a) Application is a singleton and Init/Shutdown must be called
// as a pair, and b) all unit test functions should be atomic.
[AttributeUsage (AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class AutoInitShutdown : Xunit.Sdk.BeforeAfterTestAttribute {

	static bool _init = false;
	public override void Before (MethodInfo methodUnderTest)
	{
		if (_init) {
			throw new InvalidOperationException ("After did not run.");
		}

		Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));
		_init = true;
	}

	public override void After (MethodInfo methodUnderTest)
	{
		Application.Shutdown ();
		_init = false;
	}
}