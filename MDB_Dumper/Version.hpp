#pragma once
#include <Windows.h>
#include <iostream>

#define FUNCTION_COUNT 17

#pragma region Functions
extern "C" uintptr_t functions[FUNCTION_COUNT] = { 0 };
std::string functionNames[] =
{
	"GetFileVersionInfoA",
	"GetFileVersionInfoByHandle",
	"GetFileVersionInfoExA",
	"GetFileVersionInfoExW",
	"GetFileVersionInfoSizeA",
	"GetFileVersionInfoSizeExA",
	"GetFileVersionInfoSizeExW",
	"GetFileVersionInfoSizeW",
	"GetFileVersionInfoW",
	"VerFindFileA",
	"VerFindFileW",
	"VerInstallFileA",
	"VerInstallFileW",
	"VerLanguageNameA",
	"VerLanguageNameW",
	"VerQueryValueA",
	"VerQueryValueW"
};

// [index: 000] [ordinal: 001] GetFileVersionInfoA
#pragma comment(linker, "/EXPORT:GetFileVersionInfoA=func_8a43294e40bc4a32818aa404af380bbe,@1")
extern "C" void func_8a43294e40bc4a32818aa404af380bbe();

// [index: 001] [ordinal: 002] GetFileVersionInfoByHandle
#pragma comment(linker, "/EXPORT:GetFileVersionInfoByHandle=func_25f689555e574512b14520fcbab686fe,@2")
extern "C" void func_25f689555e574512b14520fcbab686fe();

// [index: 002] [ordinal: 003] GetFileVersionInfoExA
#pragma comment(linker, "/EXPORT:GetFileVersionInfoExA=func_66519e2eb6c84bd583a078906875a253,@3")
extern "C" void func_66519e2eb6c84bd583a078906875a253();

// [index: 003] [ordinal: 004] GetFileVersionInfoExW
#pragma comment(linker, "/EXPORT:GetFileVersionInfoExW=func_cf73aa76fa9c4923bbadfca3a1af26cb,@4")
extern "C" void func_cf73aa76fa9c4923bbadfca3a1af26cb();

// [index: 004] [ordinal: 005] GetFileVersionInfoSizeA
#pragma comment(linker, "/EXPORT:GetFileVersionInfoSizeA=func_2ed0d206828e4cecbe2bbe5f565064f3,@5")
extern "C" void func_2ed0d206828e4cecbe2bbe5f565064f3();

// [index: 005] [ordinal: 006] GetFileVersionInfoSizeExA
#pragma comment(linker, "/EXPORT:GetFileVersionInfoSizeExA=func_1da0c319886943109f085a779bed4e09,@6")
extern "C" void func_1da0c319886943109f085a779bed4e09();

// [index: 006] [ordinal: 007] GetFileVersionInfoSizeExW
#pragma comment(linker, "/EXPORT:GetFileVersionInfoSizeExW=func_a3a80bd015d8450294909c01551b816f,@7")
extern "C" void func_a3a80bd015d8450294909c01551b816f();

// [index: 007] [ordinal: 008] GetFileVersionInfoSizeW
#pragma comment(linker, "/EXPORT:GetFileVersionInfoSizeW=func_d9e743de081c48719ce56cff98724a84,@8")
extern "C" void func_d9e743de081c48719ce56cff98724a84();

// [index: 008] [ordinal: 009] GetFileVersionInfoW
#pragma comment(linker, "/EXPORT:GetFileVersionInfoW=func_4e43ff6509ac476b991c2d5162e6277f,@9")
extern "C" void func_4e43ff6509ac476b991c2d5162e6277f();

// [index: 009] [ordinal: 010] VerFindFileA
#pragma comment(linker, "/EXPORT:VerFindFileA=func_1c799aa13b7847ff8fd91028f003448d,@10")
extern "C" void func_1c799aa13b7847ff8fd91028f003448d();

// [index: 010] [ordinal: 011] VerFindFileW
#pragma comment(linker, "/EXPORT:VerFindFileW=func_be8b1278f4d641b8b12fb46ad5e61d19,@11")
extern "C" void func_be8b1278f4d641b8b12fb46ad5e61d19();

// [index: 011] [ordinal: 012] VerInstallFileA
#pragma comment(linker, "/EXPORT:VerInstallFileA=func_8bfa63a71824418cb2e11e75594b79f5,@12")
extern "C" void func_8bfa63a71824418cb2e11e75594b79f5();

// [index: 012] [ordinal: 013] VerInstallFileW
#pragma comment(linker, "/EXPORT:VerInstallFileW=func_9e8010c042ca4f708523a2511ee4e3ac,@13")
extern "C" void func_9e8010c042ca4f708523a2511ee4e3ac();

// [index: 013] [ordinal: 014] VerLanguageNameA
#pragma comment(linker, "/EXPORT:VerLanguageNameA=func_595e5b4d6f664bba9ea2d47a9d74db4a,@14")
extern "C" void func_595e5b4d6f664bba9ea2d47a9d74db4a();

// [index: 014] [ordinal: 015] VerLanguageNameW
#pragma comment(linker, "/EXPORT:VerLanguageNameW=func_4022adb5507944d7be24ef9460f3a8ad,@15")
extern "C" void func_4022adb5507944d7be24ef9460f3a8ad();

// [index: 015] [ordinal: 016] VerQueryValueA
#pragma comment(linker, "/EXPORT:VerQueryValueA=func_690fe5c640504007a1839afd17a94ac2,@16")
extern "C" void func_690fe5c640504007a1839afd17a94ac2();

// [index: 016] [ordinal: 017] VerQueryValueW
#pragma comment(linker, "/EXPORT:VerQueryValueW=func_05c7ee01146545c48de653ec0b3d5269,@17")
extern "C" void func_05c7ee01146545c48de653ec0b3d5269();
#pragma endregion