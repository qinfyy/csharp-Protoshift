cd ProtoshiftBenchmark
dotnet run --configuration Release --property:DefineConstants=TRACE%3bRELEASE%3bNET%3bNET6_0%3bNETCOREAPP%3bMIHOMO_KCP%3bPROTOSHIFT_BENCHMARK -- --extra-property "DefineConstants=TRACE%3bRELEASE%3bNET%3bNET6_0%3bNETCOREAPP%3bMIHOMO_KCP%3bPROTOSHIFT_BENCHMARK" $args