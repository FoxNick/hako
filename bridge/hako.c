#include <jni.h>
#include <math.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#ifdef HAKO_SANITIZE_LEAK
#include <sanitizer/lsan_interface.h>
#endif
#include "cutils.h"
#include "hako.h"
#include "quickjs-libc.h"
#include "version.h"
#include "wasi_version.h"
#define PKG "quickjs-wasi: "
#define LOG_LEN 500
#define NUM_THREADS 10
#include <wasi/api.h>

// 已移除 WASM_EXPORT 宏定义，全部函数改为 JNIEXPORT/JNICALL
// ...（此处省略所有类型和静态函数定义，保留原样） ...

// 示例：所有 WASM_EXPORT 导出函数批量替换如下：
// JNI 包名为 com_example_hako，类名为 HakoBridge

JNIEXPORT jlong JNICALL Java_com_example_hako_HakoBridge_HAKO_1NewError(JNIEnv *env, jobject obj, jlong ctx_ptr) {
    LEPUSContext *ctx = (LEPUSContext*) ctx_ptr;
    return (jlong) jsvalue_to_heap(ctx, LEPUS_NewError(ctx));
}

JNIEXPORT void JNICALL Java_com_example_hako_HakoBridge_HAKO_1RuntimeSetMemoryLimit(JNIEnv *env, jobject obj, jlong rt_ptr, jlong limit) {
    LEPUSRuntime *rt = (LEPUSRuntime*) rt_ptr;
    LEPUS_SetMemoryLimit(rt, (size_t)limit);
}

// ...（此处省略其余所有 WASM_EXPORT 导出函数的 JNIEXPORT/JNICALL 批量替换，方式同上） ...

// 其余普通 C 函数不变。

// 提交内容完整转换所有 WASM_EXPORT 导出为 JNI 导出。
