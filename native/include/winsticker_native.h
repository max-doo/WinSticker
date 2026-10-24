#ifndef WINSTICKER_NATIVE_H
#define WINSTICKER_NATIVE_H

#include <stdint.h>

#define WINSTICKER_NATIVE_ABI_VERSION 2u
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_STRUCT_VERSION_1 1u
#define WINSTICKER_QUICK_ACCESS_STRUCT_VERSION_1 1u
#define WINSTICKER_RECYCLE_BIN_STRUCT_VERSION_1 1u
#define WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1 1u
#define WINSTICKER_NATIVE_STRUCT_VERSION_2 2u
#define WINSTICKER_NATIVE_DLL_NAME L"winsticker_native.dll"

#define WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_STORED_RAW_V2 (1ull << 0)
#define WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_EFFECTIVE_DIAGNOSTIC_V2 (1ull << 1)
#define WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_NO_UI_V2 (1ull << 2)
#define WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_WRITE_V2 (1ull << 3)
#define WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_WITH_UI_V2 (1ull << 4)
#define WINSTICKER_NATIVE_CAPABILITY_MUSIC_VOLUME_V1 (1ull << 5)
#define WINSTICKER_NATIVE_CAPABILITY_EXPLORER_SHELL_LAUNCH_V1 (1ull << 6)
#define WINSTICKER_NATIVE_CAPABILITY_QUICK_ACCESS_V1 (1ull << 7)
#define WINSTICKER_NATIVE_CAPABILITY_RECYCLE_BIN_V1 (1ull << 8)
#define WINSTICKER_NATIVE_CAPABILITIES_STAGE_3C2 \
    (WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_STORED_RAW_V2 | \
     WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_EFFECTIVE_DIAGNOSTIC_V2 | \
     WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_NO_UI_V2 | \
     WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_WRITE_V2 | \
     WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_WITH_UI_V2)
#define WINSTICKER_NATIVE_CAPABILITIES_STAGE_4C \
    (WINSTICKER_NATIVE_CAPABILITIES_STAGE_3C2 | \
     WINSTICKER_NATIVE_CAPABILITY_MUSIC_VOLUME_V1)
#define WINSTICKER_NATIVE_CAPABILITIES_STAGE_4D4A \
    (WINSTICKER_NATIVE_CAPABILITIES_STAGE_4C | \
     WINSTICKER_NATIVE_CAPABILITY_EXPLORER_SHELL_LAUNCH_V1)
#define WINSTICKER_NATIVE_CAPABILITIES_STAGE_4D4B \
    (WINSTICKER_NATIVE_CAPABILITIES_STAGE_4D4A | \
     WINSTICKER_NATIVE_CAPABILITY_QUICK_ACCESS_V1)
#define WINSTICKER_NATIVE_CAPABILITIES_STAGE_5B4C1B1 \
    (WINSTICKER_NATIVE_CAPABILITIES_STAGE_4D4B | \
     WINSTICKER_NATIVE_CAPABILITY_RECYCLE_BIN_V1)
#define WINSTICKER_NATIVE_CAPABILITIES WINSTICKER_NATIVE_CAPABILITIES_STAGE_5B4C1B1

#define WINSTICKER_NATIVE_STATUS_OK 0u
#define WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT 1u
#define WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT 2u
#define WINSTICKER_NATIVE_STATUS_BUFFER_TOO_SMALL 3u
#define WINSTICKER_NATIVE_STATUS_COM_INITIALIZATION_FAILED 4u
#define WINSTICKER_NATIVE_STATUS_OBJECT_CREATION_FAILED 5u
#define WINSTICKER_NATIVE_STATUS_LOAD_FAILED 6u
#define WINSTICKER_NATIVE_STATUS_OPERATION_FAILED 7u
#define WINSTICKER_NATIVE_STATUS_INTERNAL_ERROR 8u
#define WINSTICKER_NATIVE_STATUS_NOT_IMPLEMENTED 9u

#define WINSTICKER_NATIVE_S_OK ((int32_t)0x00000000u)
#define WINSTICKER_NATIVE_S_FALSE ((int32_t)0x00000001u)
#define WINSTICKER_NATIVE_E_NOTIMPL ((int32_t)0x80004001u)
#define WINSTICKER_NATIVE_E_INVALIDARG ((int32_t)0x80070057u)
#define WINSTICKER_NATIVE_HRESULT_INSUFFICIENT_BUFFER ((int32_t)0x8007007Au)
#define WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED ((int32_t)0x8000000Au)

#define WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW 1u
#define WINSTICKER_SHORTCUT_READ_MODE_EFFECTIVE_DIAGNOSTIC 2u
#define WINSTICKER_SHORTCUT_WRITE_FLAG_SHELL_NAMESPACE_TARGET (1u << 0)

#define WINSTICKER_SHORTCUT_FIELD_TARGET_PATH (1u << 0)
#define WINSTICKER_SHORTCUT_FIELD_DESCRIPTION (1u << 1)
#define WINSTICKER_SHORTCUT_FIELD_ARGUMENTS (1u << 2)
#define WINSTICKER_SHORTCUT_FIELD_WORKING_DIRECTORY (1u << 3)
#define WINSTICKER_SHORTCUT_FIELD_ICON_PATH (1u << 4)

#define WINSTICKER_SHORTCUT_PHASE_COM_INITIALIZE (1u << 0)
#define WINSTICKER_SHORTCUT_PHASE_CREATE_OBJECT (1u << 1)
#define WINSTICKER_SHORTCUT_PHASE_LOAD (1u << 2)
#define WINSTICKER_SHORTCUT_PHASE_RESOLVE (1u << 3)
#define WINSTICKER_SHORTCUT_PHASE_SAVE (1u << 4)

#define WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SNAPSHOT 1u
#define WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SYSTEM 2u
#define WINSTICKER_MUSIC_VOLUME_OPERATION_SET_SYSTEM 3u
#define WINSTICKER_MUSIC_VOLUME_OPERATION_SET_SESSION 4u

#define WINSTICKER_MUSIC_VOLUME_PHASE_COM_INITIALIZE (1u << 0)
#define WINSTICKER_MUSIC_VOLUME_PHASE_CREATE_ENUMERATOR (1u << 1)
#define WINSTICKER_MUSIC_VOLUME_PHASE_GET_DEVICE (1u << 2)
#define WINSTICKER_MUSIC_VOLUME_PHASE_SYSTEM_VOLUME (1u << 3)
#define WINSTICKER_MUSIC_VOLUME_PHASE_ENUMERATE_SESSIONS (1u << 4)
#define WINSTICKER_MUSIC_VOLUME_PHASE_SESSION_VOLUME (1u << 5)

#define WINSTICKER_MUSIC_VOLUME_MATCH_NONE 0u
#define WINSTICKER_MUSIC_VOLUME_MATCH_IDENTIFIER_APP_ID 1u
#define WINSTICKER_MUSIC_VOLUME_MATCH_INSTANCE_APP_ID 2u
#define WINSTICKER_MUSIC_VOLUME_MATCH_DISPLAY_NAME 3u
#define WINSTICKER_MUSIC_VOLUME_MATCH_PROCESS_DISPLAY_NAME 4u
#define WINSTICKER_MUSIC_VOLUME_MATCH_APP_ID_PROCESS 5u
#define WINSTICKER_MUSIC_VOLUME_MATCH_IDENTIFIER_DISPLAY_NAME 6u
#define WINSTICKER_MUSIC_VOLUME_MATCH_SINGLE_FALLBACK 7u

#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_COM_INITIALIZE (1u << 0)
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_CREATE_OBJECT (1u << 1)
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_WINDOWS (1u << 2)
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_DESKTOP (1u << 3)
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_DOCUMENT (1u << 4)
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_APPLICATION (1u << 5)
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_EXECUTE (1u << 6)

#define WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE 1u
#define WINSTICKER_QUICK_ACCESS_OPERATION_PIN 2u
#define WINSTICKER_QUICK_ACCESS_OPERATION_UNPIN 3u

#define WINSTICKER_QUICK_ACCESS_PIN_STATE_UNKNOWN 0u
#define WINSTICKER_QUICK_ACCESS_PIN_STATE_NOT_PINNED 1u
#define WINSTICKER_QUICK_ACCESS_PIN_STATE_PINNED 2u

#define WINSTICKER_QUICK_ACCESS_PHASE_COM_INITIALIZE (1u << 0)
#define WINSTICKER_QUICK_ACCESS_PHASE_CREATE_OBJECT (1u << 1)
#define WINSTICKER_QUICK_ACCESS_PHASE_QUICK_NAMESPACE (1u << 2)
#define WINSTICKER_QUICK_ACCESS_PHASE_ITEMS (1u << 3)
#define WINSTICKER_QUICK_ACCESS_PHASE_ENUMERATE (1u << 4)
#define WINSTICKER_QUICK_ACCESS_PHASE_ITEM_PATH (1u << 5)
#define WINSTICKER_QUICK_ACCESS_PHASE_PROPERTY (1u << 6)
#define WINSTICKER_QUICK_ACCESS_PHASE_PARENT_NAMESPACE (1u << 7)
#define WINSTICKER_QUICK_ACCESS_PHASE_PARSE_NAME (1u << 8)
#define WINSTICKER_QUICK_ACCESS_PHASE_INVOKE (1u << 9)

#define WINSTICKER_RECYCLE_BIN_OPERATION_QUERY 1u
#define WINSTICKER_RECYCLE_BIN_OPERATION_RESTORE 2u

#define WINSTICKER_RECYCLE_BIN_PHASE_COM_INITIALIZE (1u << 0)
#define WINSTICKER_RECYCLE_BIN_PHASE_CREATE_OBJECT (1u << 1)
#define WINSTICKER_RECYCLE_BIN_PHASE_NAMESPACE (1u << 2)
#define WINSTICKER_RECYCLE_BIN_PHASE_ITEMS (1u << 3)
#define WINSTICKER_RECYCLE_BIN_PHASE_ENUMERATE (1u << 4)
#define WINSTICKER_RECYCLE_BIN_PHASE_ITEM_NAME (1u << 5)
#define WINSTICKER_RECYCLE_BIN_PHASE_PROPERTY (1u << 6)
#define WINSTICKER_RECYCLE_BIN_PHASE_INVOKE (1u << 7)

#define WINSTICKER_SHORTCUT_DEFAULT_RESOLVE_TIMEOUT_MS 3000u
#define WINSTICKER_SHORTCUT_MAX_RESOLVE_TIMEOUT_MS 65535u
#define WINSTICKER_SHORTCUT_MAX_INPUT_PATH_CHARS 32767u
#define WINSTICKER_SHORTCUT_MAX_INPUT_VALUE_CHARS 32767u
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_MAX_INPUT_CHARS 32767u
#define WINSTICKER_QUICK_ACCESS_MAX_INPUT_CHARS 32767u
#define WINSTICKER_RECYCLE_BIN_MAX_INPUT_CHARS 32767u

#define WINSTICKER_NATIVE_UTF16_BUFFER_V1_SIZE_64 16u
#define WINSTICKER_NATIVE_UTF16_STRING_V1_SIZE_64 16u
#define WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64 144u
#define WINSTICKER_SHORTCUT_READ_RESULT_V2_SIZE_64 136u
#define WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64 192u
#define WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64 144u
#define WINSTICKER_SHORTCUT_WRITE_RESULT_V2_SIZE_64 96u
#define WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64 64u
#define WINSTICKER_SHORTCUT_UI_RESOLVE_RESULT_V2_SIZE_64 64u
#define WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64 88u
#define WINSTICKER_MUSIC_VOLUME_RESULT_V1_SIZE_64 104u
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_REQUEST_V1_SIZE_64 96u
#define WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64 88u
#define WINSTICKER_QUICK_ACCESS_REQUEST_V1_SIZE_64 96u
#define WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64 112u
#define WINSTICKER_RECYCLE_BIN_REQUEST_V1_SIZE_64 80u
#define WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64 104u

#if defined(_WIN32)
#define WINSTICKER_NATIVE_API __declspec(dllimport)
#define WINSTICKER_NATIVE_CALL __cdecl
#else
#define WINSTICKER_NATIVE_API
#define WINSTICKER_NATIVE_CALL
#endif

typedef struct WinStickerNativeUtf16BufferV1 {
    uint16_t* data;
    uint32_t capacity_chars;
    uint32_t reserved0;
} WinStickerNativeUtf16BufferV1;

typedef struct WinStickerNativeUtf16StringV1 {
    const uint16_t* data;
    uint32_t length_chars;
    uint32_t reserved0;
} WinStickerNativeUtf16StringV1;

typedef struct WinStickerShortcutReadRequestV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t mode;
    uint32_t flags;
    const uint16_t* shortcut_path;
    uint32_t shortcut_path_length_chars;
    uint32_t reserved0;
    WinStickerNativeUtf16BufferV1 target_path;
    WinStickerNativeUtf16BufferV1 description;
    WinStickerNativeUtf16BufferV1 arguments;
    WinStickerNativeUtf16BufferV1 working_directory;
    WinStickerNativeUtf16BufferV1 icon_path;
    uint64_t reserved[4];
} WinStickerShortcutReadRequestV2;

typedef struct WinStickerShortcutReadResultV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t load_hresult;
    int32_t resolve_hresult;
    uint32_t attempted_fields;
    uint32_t succeeded_fields;
    uint32_t present_fields;
    uint32_t caller_buffer_too_small_fields;
    uint32_t source_truncated_fields;
    int32_t target_hresult;
    int32_t description_hresult;
    int32_t arguments_hresult;
    int32_t working_directory_hresult;
    int32_t icon_hresult;
    int32_t icon_index;
    uint32_t target_required_chars;
    uint32_t description_required_chars;
    uint32_t arguments_required_chars;
    uint32_t working_directory_required_chars;
    uint32_t icon_required_chars;
    uint32_t reserved0;
    uint64_t reserved[4];
} WinStickerShortcutReadResultV2;

typedef struct WinStickerShortcutResolveRequestV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t timeout_ms;
    uint32_t flags;
    WinStickerShortcutReadRequestV2 read_request;
    uint64_t reserved[4];
} WinStickerShortcutResolveRequestV2;

typedef struct WinStickerShortcutWriteRequestV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t flags;
    int32_t icon_index;
    WinStickerNativeUtf16StringV1 shortcut_path;
    WinStickerNativeUtf16StringV1 target_path;
    WinStickerNativeUtf16StringV1 description;
    WinStickerNativeUtf16StringV1 arguments;
    WinStickerNativeUtf16StringV1 working_directory;
    WinStickerNativeUtf16StringV1 icon_path;
    uint64_t reserved[4];
} WinStickerShortcutWriteRequestV2;

typedef struct WinStickerShortcutWriteResultV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t save_hresult;
    uint32_t attempted_fields;
    uint32_t succeeded_fields;
    int32_t target_hresult;
    int32_t description_hresult;
    int32_t arguments_hresult;
    int32_t working_directory_hresult;
    int32_t icon_hresult;
    uint32_t reserved0;
    uint64_t reserved[4];
} WinStickerShortcutWriteResultV2;

typedef struct WinStickerShortcutUiResolveRequestV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t flags;
    uint32_t reserved0;
    WinStickerNativeUtf16StringV1 shortcut_path;
    uint64_t owner_hwnd;
    uint64_t reserved[3];
} WinStickerShortcutUiResolveRequestV2;

typedef struct WinStickerShortcutUiResolveResultV2 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t load_hresult;
    int32_t resolve_hresult;
    uint32_t resolve_flags;
    uint64_t reserved[3];
} WinStickerShortcutUiResolveResultV2;

typedef struct WinStickerMusicVolumeRequestV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t operation;
    uint32_t flags;
    WinStickerNativeUtf16StringV1 source_app_user_model_id;
    WinStickerNativeUtf16StringV1 source_display_name;
    double volume;
    uint64_t reserved[4];
} WinStickerMusicVolumeRequestV1;

typedef struct WinStickerMusicVolumeResultV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    uint32_t match_kind;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t device_hresult;
    int32_t system_hresult;
    int32_t session_hresult;
    uint32_t has_session_volume;
    uint32_t operation_succeeded;
    uint32_t reserved0;
    double system_volume;
    double session_volume;
    uint64_t reserved[4];
} WinStickerMusicVolumeResultV1;

typedef struct WinStickerExplorerShellLaunchRequestV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t flags;
    uint32_t reserved0;
    WinStickerNativeUtf16StringV1 path;
    WinStickerNativeUtf16StringV1 working_directory;
    WinStickerNativeUtf16StringV1 verb;
    uint64_t reserved[4];
} WinStickerExplorerShellLaunchRequestV1;

typedef struct WinStickerExplorerShellLaunchResultV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t windows_hresult;
    int32_t desktop_hresult;
    int32_t document_hresult;
    int32_t application_hresult;
    int32_t execute_hresult;
    uint32_t operation_succeeded;
    uint32_t reserved0;
    uint64_t reserved[4];
} WinStickerExplorerShellLaunchResultV1;

typedef struct WinStickerQuickAccessRequestV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t operation;
    uint32_t flags;
    WinStickerNativeUtf16StringV1 folder_path;
    WinStickerNativeUtf16StringV1 parent_path;
    WinStickerNativeUtf16StringV1 folder_name;
    uint64_t reserved[4];
} WinStickerQuickAccessRequestV1;

typedef struct WinStickerQuickAccessResultV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t quick_namespace_hresult;
    int32_t items_hresult;
    int32_t enumerate_hresult;
    int32_t item_path_hresult;
    int32_t property_hresult;
    int32_t parent_namespace_hresult;
    int32_t parse_name_hresult;
    int32_t invoke_hresult;
    uint32_t pin_state;
    uint32_t operation_succeeded;
    uint32_t matched_item;
    uint32_t fallback_used;
    uint32_t reserved0;
    uint64_t reserved[4];
} WinStickerQuickAccessResultV1;

typedef struct WinStickerRecycleBinRequestV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t operation;
    uint32_t flags;
    WinStickerNativeUtf16StringV1 original_parent;
    WinStickerNativeUtf16StringV1 original_name;
    uint64_t reserved[4];
} WinStickerRecycleBinRequestV1;

typedef struct WinStickerRecycleBinResultV1 {
    uint32_t struct_size;
    uint32_t struct_version;
    uint32_t status;
    int32_t operation_hresult;
    uint32_t attempted_phases;
    int32_t com_hresult;
    int32_t create_hresult;
    int32_t namespace_hresult;
    int32_t items_hresult;
    int32_t enumerate_hresult;
    int32_t item_name_hresult;
    int32_t property_hresult;
    int32_t invoke_hresult;
    uint32_t matched_count;
    uint32_t restored_count;
    uint32_t operation_succeeded;
    uint32_t reserved0;
    uint32_t reserved1;
    uint64_t reserved[4];
} WinStickerRecycleBinResultV1;

#if UINTPTR_MAX == UINT64_MAX
#if defined(__cplusplus)
static_assert(sizeof(WinStickerNativeUtf16BufferV1) == WINSTICKER_NATIVE_UTF16_BUFFER_V1_SIZE_64,
              "WinStickerNativeUtf16BufferV1 ABI size changed");
static_assert(sizeof(WinStickerNativeUtf16StringV1) == WINSTICKER_NATIVE_UTF16_STRING_V1_SIZE_64,
              "WinStickerNativeUtf16StringV1 ABI size changed");
static_assert(sizeof(WinStickerShortcutReadRequestV2) == WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64,
              "WinStickerShortcutReadRequestV2 ABI size changed");
static_assert(sizeof(WinStickerShortcutReadResultV2) == WINSTICKER_SHORTCUT_READ_RESULT_V2_SIZE_64,
              "WinStickerShortcutReadResultV2 ABI size changed");
static_assert(sizeof(WinStickerShortcutResolveRequestV2) == WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64,
              "WinStickerShortcutResolveRequestV2 ABI size changed");
static_assert(sizeof(WinStickerShortcutWriteRequestV2) == WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64,
              "WinStickerShortcutWriteRequestV2 ABI size changed");
static_assert(sizeof(WinStickerShortcutWriteResultV2) == WINSTICKER_SHORTCUT_WRITE_RESULT_V2_SIZE_64,
              "WinStickerShortcutWriteResultV2 ABI size changed");
static_assert(sizeof(WinStickerShortcutUiResolveRequestV2) == WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64,
              "WinStickerShortcutUiResolveRequestV2 ABI size changed");
static_assert(sizeof(WinStickerShortcutUiResolveResultV2) == WINSTICKER_SHORTCUT_UI_RESOLVE_RESULT_V2_SIZE_64,
              "WinStickerShortcutUiResolveResultV2 ABI size changed");
static_assert(sizeof(WinStickerMusicVolumeRequestV1) == WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64,
              "WinStickerMusicVolumeRequestV1 ABI size changed");
static_assert(sizeof(WinStickerMusicVolumeResultV1) == WINSTICKER_MUSIC_VOLUME_RESULT_V1_SIZE_64,
              "WinStickerMusicVolumeResultV1 ABI size changed");
static_assert(sizeof(WinStickerExplorerShellLaunchRequestV1) == WINSTICKER_EXPLORER_SHELL_LAUNCH_REQUEST_V1_SIZE_64,
              "WinStickerExplorerShellLaunchRequestV1 ABI size changed");
static_assert(sizeof(WinStickerExplorerShellLaunchResultV1) == WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64,
              "WinStickerExplorerShellLaunchResultV1 ABI size changed");
static_assert(sizeof(WinStickerQuickAccessRequestV1) == WINSTICKER_QUICK_ACCESS_REQUEST_V1_SIZE_64,
              "WinStickerQuickAccessRequestV1 ABI size changed");
static_assert(sizeof(WinStickerQuickAccessResultV1) == WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64,
              "WinStickerQuickAccessResultV1 ABI size changed");
static_assert(sizeof(WinStickerRecycleBinRequestV1) == WINSTICKER_RECYCLE_BIN_REQUEST_V1_SIZE_64,
              "WinStickerRecycleBinRequestV1 ABI size changed");
static_assert(sizeof(WinStickerRecycleBinResultV1) == WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64,
              "WinStickerRecycleBinResultV1 ABI size changed");
#elif defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
_Static_assert(sizeof(WinStickerNativeUtf16BufferV1) == WINSTICKER_NATIVE_UTF16_BUFFER_V1_SIZE_64,
               "WinStickerNativeUtf16BufferV1 ABI size changed");
_Static_assert(sizeof(WinStickerNativeUtf16StringV1) == WINSTICKER_NATIVE_UTF16_STRING_V1_SIZE_64,
               "WinStickerNativeUtf16StringV1 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutReadRequestV2) == WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64,
               "WinStickerShortcutReadRequestV2 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutReadResultV2) == WINSTICKER_SHORTCUT_READ_RESULT_V2_SIZE_64,
               "WinStickerShortcutReadResultV2 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutResolveRequestV2) == WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64,
               "WinStickerShortcutResolveRequestV2 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutWriteRequestV2) == WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64,
               "WinStickerShortcutWriteRequestV2 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutWriteResultV2) == WINSTICKER_SHORTCUT_WRITE_RESULT_V2_SIZE_64,
               "WinStickerShortcutWriteResultV2 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutUiResolveRequestV2) == WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64,
               "WinStickerShortcutUiResolveRequestV2 ABI size changed");
_Static_assert(sizeof(WinStickerShortcutUiResolveResultV2) == WINSTICKER_SHORTCUT_UI_RESOLVE_RESULT_V2_SIZE_64,
               "WinStickerShortcutUiResolveResultV2 ABI size changed");
_Static_assert(sizeof(WinStickerMusicVolumeRequestV1) == WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64,
               "WinStickerMusicVolumeRequestV1 ABI size changed");
_Static_assert(sizeof(WinStickerMusicVolumeResultV1) == WINSTICKER_MUSIC_VOLUME_RESULT_V1_SIZE_64,
               "WinStickerMusicVolumeResultV1 ABI size changed");
_Static_assert(sizeof(WinStickerExplorerShellLaunchRequestV1) == WINSTICKER_EXPLORER_SHELL_LAUNCH_REQUEST_V1_SIZE_64,
               "WinStickerExplorerShellLaunchRequestV1 ABI size changed");
_Static_assert(sizeof(WinStickerExplorerShellLaunchResultV1) == WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64,
               "WinStickerExplorerShellLaunchResultV1 ABI size changed");
_Static_assert(sizeof(WinStickerQuickAccessRequestV1) == WINSTICKER_QUICK_ACCESS_REQUEST_V1_SIZE_64,
               "WinStickerQuickAccessRequestV1 ABI size changed");
_Static_assert(sizeof(WinStickerQuickAccessResultV1) == WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64,
               "WinStickerQuickAccessResultV1 ABI size changed");
_Static_assert(sizeof(WinStickerRecycleBinRequestV1) == WINSTICKER_RECYCLE_BIN_REQUEST_V1_SIZE_64,
               "WinStickerRecycleBinRequestV1 ABI size changed");
_Static_assert(sizeof(WinStickerRecycleBinResultV1) == WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64,
               "WinStickerRecycleBinResultV1 ABI size changed");
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_native_abi_version(void);
WINSTICKER_NATIVE_API uint64_t WINSTICKER_NATIVE_CALL winsticker_native_capabilities(void);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_shortcut_read_v2(
    const WinStickerShortcutReadRequestV2* request,
    WinStickerShortcutReadResultV2* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_shortcut_resolve_no_ui_v2(
    const WinStickerShortcutResolveRequestV2* request,
    WinStickerShortcutReadResultV2* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_shortcut_write_v2(
    const WinStickerShortcutWriteRequestV2* request,
    WinStickerShortcutWriteResultV2* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_shortcut_resolve_with_ui_v2(
    const WinStickerShortcutUiResolveRequestV2* request,
    WinStickerShortcutUiResolveResultV2* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_music_volume_v1(
    const WinStickerMusicVolumeRequestV1* request,
    WinStickerMusicVolumeResultV1* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_explorer_shell_launch_v1(
    const WinStickerExplorerShellLaunchRequestV1* request,
    WinStickerExplorerShellLaunchResultV1* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_quick_access_v1(
    const WinStickerQuickAccessRequestV1* request,
    WinStickerQuickAccessResultV1* result);
WINSTICKER_NATIVE_API uint32_t WINSTICKER_NATIVE_CALL winsticker_recycle_bin_v1(
    const WinStickerRecycleBinRequestV1* request,
    WinStickerRecycleBinResultV1* result);

#ifdef __cplusplus
}
#endif

#endif
