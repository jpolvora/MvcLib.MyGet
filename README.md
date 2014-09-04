[![jpolvora MyGet Build Status](https://www.myget.org/BuildSource/Badge/jpolvora?identifier=4b02c1ad-9595-42aa-bf28-4fc1c091470e)](https://www.myget.org/)

MvcLib.MyGet
============

#Repository to build [MvcLib](https://github.com/jpolvora/MvcLib) Nuget Package


This repository contains a main project (Packager) that references MvcLib projects (subtree) in order to build a NuGet package.

MyGet will build and publish the final package.


https://www.myget.org/BuildSource/List/jpolvora


https://www.myget.org/feed/jpolvora/package/MvcLib.Nuget


#NuGet.Config
```xml
	<configuration>
	  <solution>
		<add key="disableSourceControlIntegration" value="true" />
	  </solution>
	  <packageRestore>
		<add key="enabled" value="True" />
		<add key="automatic" value="True" />
	  </packageRestore>
	  <packageSources>
		<add key="nuget.org" value="https://www.nuget.org/api/v2/" />
		<add key="MyGet" value="https://www.myget.org/F/jpolvora/api/v2" />
	  </packageSources>
	  <disabledPackageSources />
	  <activePackageSource>
		<add key="All" value="(Aggregate source)" />
	  </activePackageSource>
	</configuration>
```