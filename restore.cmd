echo off
echo options: debug (default), release
echo args: %1%

set bconfig=debug
if '%1' NEQ '' (set bconfig=%1%)
echo config: %bconfig%

Rem -- todo: add adal stuff

msbuild Combined.NoWinRT.sln /t:restore /p:configuration=%bconfig%