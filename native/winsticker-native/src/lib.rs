//! Stable native ABI boundary for WinSticker.
//!
//! The module implements the versioned shortcut, music-volume, Explorer-hosted
//! launch, and Quick Access boundaries used by WinSticker Native AOT builds.
//! Callers must check the capability mask before invoking an operation export.

use std::mem::size_of;

mod explorer_shell_launch;
mod music_volume;
mod quick_access;
mod recycle_bin;
mod shortcut;

pub const WINSTICKER_NATIVE_ABI_VERSION: u32 = 2;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_STRUCT_VERSION_1: u32 = 1;
pub const WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1: u32 = 1;
pub const WINSTICKER_QUICK_ACCESS_STRUCT_VERSION_1: u32 = 1;
pub const WINSTICKER_RECYCLE_BIN_STRUCT_VERSION_1: u32 = 1;
pub const WINSTICKER_NATIVE_STRUCT_VERSION_2: u32 = 2;

pub const WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_STORED_RAW_V2: u64 = 1 << 0;
pub const WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_EFFECTIVE_DIAGNOSTIC_V2: u64 = 1 << 1;
pub const WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_NO_UI_V2: u64 = 1 << 2;
pub const WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_WRITE_V2: u64 = 1 << 3;
pub const WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_WITH_UI_V2: u64 = 1 << 4;
pub const WINSTICKER_NATIVE_CAPABILITY_MUSIC_VOLUME_V1: u64 = 1 << 5;
pub const WINSTICKER_NATIVE_CAPABILITY_EXPLORER_SHELL_LAUNCH_V1: u64 = 1 << 6;
pub const WINSTICKER_NATIVE_CAPABILITY_QUICK_ACCESS_V1: u64 = 1 << 7;
pub const WINSTICKER_NATIVE_CAPABILITY_RECYCLE_BIN_V1: u64 = 1 << 8;

/// Stage 4D-4B enables every prior operation plus the Quick Access boundary.
pub const WINSTICKER_NATIVE_CAPABILITIES: u64 = WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_STORED_RAW_V2
    | WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_READ_EFFECTIVE_DIAGNOSTIC_V2
    | WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_NO_UI_V2
    | WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_WRITE_V2
    | WINSTICKER_NATIVE_CAPABILITY_SHORTCUT_RESOLVE_WITH_UI_V2
    | WINSTICKER_NATIVE_CAPABILITY_MUSIC_VOLUME_V1
    | WINSTICKER_NATIVE_CAPABILITY_EXPLORER_SHELL_LAUNCH_V1
    | WINSTICKER_NATIVE_CAPABILITY_QUICK_ACCESS_V1
    | WINSTICKER_NATIVE_CAPABILITY_RECYCLE_BIN_V1;

pub const WINSTICKER_NATIVE_STATUS_OK: u32 = 0;
pub const WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT: u32 = 1;
pub const WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT: u32 = 2;
pub const WINSTICKER_NATIVE_STATUS_BUFFER_TOO_SMALL: u32 = 3;
pub const WINSTICKER_NATIVE_STATUS_COM_INITIALIZATION_FAILED: u32 = 4;
pub const WINSTICKER_NATIVE_STATUS_OBJECT_CREATION_FAILED: u32 = 5;
pub const WINSTICKER_NATIVE_STATUS_LOAD_FAILED: u32 = 6;
pub const WINSTICKER_NATIVE_STATUS_OPERATION_FAILED: u32 = 7;
pub const WINSTICKER_NATIVE_STATUS_INTERNAL_ERROR: u32 = 8;
pub const WINSTICKER_NATIVE_STATUS_NOT_IMPLEMENTED: u32 = 9;

pub const WINSTICKER_NATIVE_S_OK: i32 = 0;
pub const WINSTICKER_NATIVE_S_FALSE: i32 = 1;
pub const WINSTICKER_NATIVE_E_NOTIMPL: i32 = 0x8000_4001_u32 as i32;
pub const WINSTICKER_NATIVE_E_INVALIDARG: i32 = 0x8007_0057_u32 as i32;
pub const WINSTICKER_NATIVE_HRESULT_INSUFFICIENT_BUFFER: i32 = 0x8007_007A_u32 as i32;
pub const WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED: i32 = 0x8000_000A_u32 as i32;

pub const WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW: u32 = 1;
pub const WINSTICKER_SHORTCUT_READ_MODE_EFFECTIVE_DIAGNOSTIC: u32 = 2;
pub const WINSTICKER_SHORTCUT_WRITE_FLAG_SHELL_NAMESPACE_TARGET: u32 = 1 << 0;

pub const WINSTICKER_SHORTCUT_FIELD_TARGET_PATH: u32 = 1 << 0;
pub const WINSTICKER_SHORTCUT_FIELD_DESCRIPTION: u32 = 1 << 1;
pub const WINSTICKER_SHORTCUT_FIELD_ARGUMENTS: u32 = 1 << 2;
pub const WINSTICKER_SHORTCUT_FIELD_WORKING_DIRECTORY: u32 = 1 << 3;
pub const WINSTICKER_SHORTCUT_FIELD_ICON_PATH: u32 = 1 << 4;

pub const WINSTICKER_SHORTCUT_PHASE_COM_INITIALIZE: u32 = 1 << 0;
pub const WINSTICKER_SHORTCUT_PHASE_CREATE_OBJECT: u32 = 1 << 1;
pub const WINSTICKER_SHORTCUT_PHASE_LOAD: u32 = 1 << 2;
pub const WINSTICKER_SHORTCUT_PHASE_RESOLVE: u32 = 1 << 3;
pub const WINSTICKER_SHORTCUT_PHASE_SAVE: u32 = 1 << 4;

pub const WINSTICKER_SHORTCUT_MAX_INPUT_PATH_CHARS: u32 = 32_767;
pub const WINSTICKER_SHORTCUT_MAX_INPUT_VALUE_CHARS: u32 = 32_767;

pub const WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SNAPSHOT: u32 = 1;
pub const WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SYSTEM: u32 = 2;
pub const WINSTICKER_MUSIC_VOLUME_OPERATION_SET_SYSTEM: u32 = 3;
pub const WINSTICKER_MUSIC_VOLUME_OPERATION_SET_SESSION: u32 = 4;

pub const WINSTICKER_MUSIC_VOLUME_PHASE_COM_INITIALIZE: u32 = 1 << 0;
pub const WINSTICKER_MUSIC_VOLUME_PHASE_CREATE_ENUMERATOR: u32 = 1 << 1;
pub const WINSTICKER_MUSIC_VOLUME_PHASE_GET_DEVICE: u32 = 1 << 2;
pub const WINSTICKER_MUSIC_VOLUME_PHASE_SYSTEM_VOLUME: u32 = 1 << 3;
pub const WINSTICKER_MUSIC_VOLUME_PHASE_ENUMERATE_SESSIONS: u32 = 1 << 4;
pub const WINSTICKER_MUSIC_VOLUME_PHASE_SESSION_VOLUME: u32 = 1 << 5;

pub const WINSTICKER_MUSIC_VOLUME_MATCH_NONE: u32 = 0;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_IDENTIFIER_APP_ID: u32 = 1;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_INSTANCE_APP_ID: u32 = 2;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_DISPLAY_NAME: u32 = 3;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_PROCESS_DISPLAY_NAME: u32 = 4;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_APP_ID_PROCESS: u32 = 5;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_IDENTIFIER_DISPLAY_NAME: u32 = 6;
pub const WINSTICKER_MUSIC_VOLUME_MATCH_SINGLE_FALLBACK: u32 = 7;

pub const WINSTICKER_MUSIC_VOLUME_MAX_INPUT_CHARS: u32 = 32_767;

pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_COM_INITIALIZE: u32 = 1 << 0;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_CREATE_OBJECT: u32 = 1 << 1;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_WINDOWS: u32 = 1 << 2;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_DESKTOP: u32 = 1 << 3;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_DOCUMENT: u32 = 1 << 4;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_APPLICATION: u32 = 1 << 5;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_PHASE_EXECUTE: u32 = 1 << 6;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_MAX_INPUT_CHARS: u32 = 32_767;

pub const WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE: u32 = 1;
pub const WINSTICKER_QUICK_ACCESS_OPERATION_PIN: u32 = 2;
pub const WINSTICKER_QUICK_ACCESS_OPERATION_UNPIN: u32 = 3;

pub const WINSTICKER_QUICK_ACCESS_PIN_STATE_UNKNOWN: u32 = 0;
pub const WINSTICKER_QUICK_ACCESS_PIN_STATE_NOT_PINNED: u32 = 1;
pub const WINSTICKER_QUICK_ACCESS_PIN_STATE_PINNED: u32 = 2;

pub const WINSTICKER_QUICK_ACCESS_PHASE_COM_INITIALIZE: u32 = 1 << 0;
pub const WINSTICKER_QUICK_ACCESS_PHASE_CREATE_OBJECT: u32 = 1 << 1;
pub const WINSTICKER_QUICK_ACCESS_PHASE_QUICK_NAMESPACE: u32 = 1 << 2;
pub const WINSTICKER_QUICK_ACCESS_PHASE_ITEMS: u32 = 1 << 3;
pub const WINSTICKER_QUICK_ACCESS_PHASE_ENUMERATE: u32 = 1 << 4;
pub const WINSTICKER_QUICK_ACCESS_PHASE_ITEM_PATH: u32 = 1 << 5;
pub const WINSTICKER_QUICK_ACCESS_PHASE_PROPERTY: u32 = 1 << 6;
pub const WINSTICKER_QUICK_ACCESS_PHASE_PARENT_NAMESPACE: u32 = 1 << 7;
pub const WINSTICKER_QUICK_ACCESS_PHASE_PARSE_NAME: u32 = 1 << 8;
pub const WINSTICKER_QUICK_ACCESS_PHASE_INVOKE: u32 = 1 << 9;
pub const WINSTICKER_QUICK_ACCESS_MAX_INPUT_CHARS: u32 = 32_767;

pub const WINSTICKER_RECYCLE_BIN_OPERATION_QUERY: u32 = 1;
pub const WINSTICKER_RECYCLE_BIN_OPERATION_RESTORE: u32 = 2;

pub const WINSTICKER_RECYCLE_BIN_PHASE_COM_INITIALIZE: u32 = 1 << 0;
pub const WINSTICKER_RECYCLE_BIN_PHASE_CREATE_OBJECT: u32 = 1 << 1;
pub const WINSTICKER_RECYCLE_BIN_PHASE_NAMESPACE: u32 = 1 << 2;
pub const WINSTICKER_RECYCLE_BIN_PHASE_ITEMS: u32 = 1 << 3;
pub const WINSTICKER_RECYCLE_BIN_PHASE_ENUMERATE: u32 = 1 << 4;
pub const WINSTICKER_RECYCLE_BIN_PHASE_ITEM_NAME: u32 = 1 << 5;
pub const WINSTICKER_RECYCLE_BIN_PHASE_PROPERTY: u32 = 1 << 6;
pub const WINSTICKER_RECYCLE_BIN_PHASE_INVOKE: u32 = 1 << 7;
pub const WINSTICKER_RECYCLE_BIN_MAX_INPUT_CHARS: u32 = 32_767;

pub const WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64: u32 = 144;
pub const WINSTICKER_SHORTCUT_READ_RESULT_V2_SIZE_64: u32 = 136;
pub const WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64: u32 = 192;
pub const WINSTICKER_NATIVE_UTF16_STRING_V1_SIZE_64: u32 = 16;
pub const WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64: u32 = 144;
pub const WINSTICKER_SHORTCUT_WRITE_RESULT_V2_SIZE_64: u32 = 96;
pub const WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64: u32 = 64;
pub const WINSTICKER_SHORTCUT_UI_RESOLVE_RESULT_V2_SIZE_64: u32 = 64;
pub const WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64: u32 = 88;
pub const WINSTICKER_MUSIC_VOLUME_RESULT_V1_SIZE_64: u32 = 104;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_REQUEST_V1_SIZE_64: u32 = 96;
pub const WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64: u32 = 88;
pub const WINSTICKER_QUICK_ACCESS_REQUEST_V1_SIZE_64: u32 = 96;
pub const WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64: u32 = 112;
pub const WINSTICKER_RECYCLE_BIN_REQUEST_V1_SIZE_64: u32 = 80;
pub const WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64: u32 = 104;

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerNativeUtf16BufferV1 {
    pub data: *mut u16,
    pub capacity_chars: u32,
    pub reserved0: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerNativeUtf16StringV1 {
    pub data: *const u16,
    pub length_chars: u32,
    pub reserved0: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutReadRequestV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub mode: u32,
    pub flags: u32,
    pub shortcut_path: *const u16,
    pub shortcut_path_length_chars: u32,
    pub reserved0: u32,
    pub target_path: WinStickerNativeUtf16BufferV1,
    pub description: WinStickerNativeUtf16BufferV1,
    pub arguments: WinStickerNativeUtf16BufferV1,
    pub working_directory: WinStickerNativeUtf16BufferV1,
    pub icon_path: WinStickerNativeUtf16BufferV1,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutReadResultV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub load_hresult: i32,
    pub resolve_hresult: i32,
    pub attempted_fields: u32,
    pub succeeded_fields: u32,
    pub present_fields: u32,
    pub caller_buffer_too_small_fields: u32,
    pub source_truncated_fields: u32,
    pub target_hresult: i32,
    pub description_hresult: i32,
    pub arguments_hresult: i32,
    pub working_directory_hresult: i32,
    pub icon_hresult: i32,
    pub icon_index: i32,
    pub target_required_chars: u32,
    pub description_required_chars: u32,
    pub arguments_required_chars: u32,
    pub working_directory_required_chars: u32,
    pub icon_required_chars: u32,
    pub reserved0: u32,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutResolveRequestV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub timeout_ms: u32,
    pub flags: u32,
    pub read_request: WinStickerShortcutReadRequestV2,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutWriteRequestV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub flags: u32,
    pub icon_index: i32,
    pub shortcut_path: WinStickerNativeUtf16StringV1,
    pub target_path: WinStickerNativeUtf16StringV1,
    pub description: WinStickerNativeUtf16StringV1,
    pub arguments: WinStickerNativeUtf16StringV1,
    pub working_directory: WinStickerNativeUtf16StringV1,
    pub icon_path: WinStickerNativeUtf16StringV1,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutWriteResultV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub save_hresult: i32,
    pub attempted_fields: u32,
    pub succeeded_fields: u32,
    pub target_hresult: i32,
    pub description_hresult: i32,
    pub arguments_hresult: i32,
    pub working_directory_hresult: i32,
    pub icon_hresult: i32,
    pub reserved0: u32,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutUiResolveRequestV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub flags: u32,
    pub reserved0: u32,
    pub shortcut_path: WinStickerNativeUtf16StringV1,
    pub owner_hwnd: u64,
    pub reserved: [u64; 3],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerShortcutUiResolveResultV2 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub load_hresult: i32,
    pub resolve_hresult: i32,
    pub resolve_flags: u32,
    pub reserved: [u64; 3],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerMusicVolumeRequestV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub operation: u32,
    pub flags: u32,
    pub source_app_user_model_id: WinStickerNativeUtf16StringV1,
    pub source_display_name: WinStickerNativeUtf16StringV1,
    pub volume: f64,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerMusicVolumeResultV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub match_kind: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub device_hresult: i32,
    pub system_hresult: i32,
    pub session_hresult: i32,
    pub has_session_volume: u32,
    pub operation_succeeded: u32,
    pub reserved0: u32,
    pub system_volume: f64,
    pub session_volume: f64,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerExplorerShellLaunchRequestV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub flags: u32,
    pub reserved0: u32,
    pub path: WinStickerNativeUtf16StringV1,
    pub working_directory: WinStickerNativeUtf16StringV1,
    pub verb: WinStickerNativeUtf16StringV1,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerExplorerShellLaunchResultV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub windows_hresult: i32,
    pub desktop_hresult: i32,
    pub document_hresult: i32,
    pub application_hresult: i32,
    pub execute_hresult: i32,
    pub operation_succeeded: u32,
    pub reserved0: u32,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerQuickAccessRequestV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub operation: u32,
    pub flags: u32,
    pub folder_path: WinStickerNativeUtf16StringV1,
    pub parent_path: WinStickerNativeUtf16StringV1,
    pub folder_name: WinStickerNativeUtf16StringV1,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerQuickAccessResultV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub quick_namespace_hresult: i32,
    pub items_hresult: i32,
    pub enumerate_hresult: i32,
    pub item_path_hresult: i32,
    pub property_hresult: i32,
    pub parent_namespace_hresult: i32,
    pub parse_name_hresult: i32,
    pub invoke_hresult: i32,
    pub pin_state: u32,
    pub operation_succeeded: u32,
    pub matched_item: u32,
    pub fallback_used: u32,
    pub reserved0: u32,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerRecycleBinRequestV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub operation: u32,
    pub flags: u32,
    pub original_parent: WinStickerNativeUtf16StringV1,
    pub original_name: WinStickerNativeUtf16StringV1,
    pub reserved: [u64; 4],
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct WinStickerRecycleBinResultV1 {
    pub struct_size: u32,
    pub struct_version: u32,
    pub status: u32,
    pub operation_hresult: i32,
    pub attempted_phases: u32,
    pub com_hresult: i32,
    pub create_hresult: i32,
    pub namespace_hresult: i32,
    pub items_hresult: i32,
    pub enumerate_hresult: i32,
    pub item_name_hresult: i32,
    pub property_hresult: i32,
    pub invoke_hresult: i32,
    pub matched_count: u32,
    pub restored_count: u32,
    pub operation_succeeded: u32,
    pub reserved0: u32,
    pub reserved1: u32,
    pub reserved: [u64; 4],
}

const fn empty_result() -> WinStickerShortcutReadResultV2 {
    WinStickerShortcutReadResultV2 {
        struct_size: WINSTICKER_SHORTCUT_READ_RESULT_V2_SIZE_64,
        struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        load_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        resolve_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        attempted_fields: 0,
        succeeded_fields: 0,
        present_fields: 0,
        caller_buffer_too_small_fields: 0,
        source_truncated_fields: 0,
        target_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        description_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        arguments_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        working_directory_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        icon_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        icon_index: 0,
        target_required_chars: 0,
        description_required_chars: 0,
        arguments_required_chars: 0,
        working_directory_required_chars: 0,
        icon_required_chars: 0,
        reserved0: 0,
        reserved: [0; 4],
    }
}

const fn empty_write_result() -> WinStickerShortcutWriteResultV2 {
    WinStickerShortcutWriteResultV2 {
        struct_size: WINSTICKER_SHORTCUT_WRITE_RESULT_V2_SIZE_64,
        struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        save_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        attempted_fields: 0,
        succeeded_fields: 0,
        target_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        description_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        arguments_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        working_directory_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        icon_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        reserved0: 0,
        reserved: [0; 4],
    }
}

const fn empty_ui_resolve_result() -> WinStickerShortcutUiResolveResultV2 {
    WinStickerShortcutUiResolveResultV2 {
        struct_size: WINSTICKER_SHORTCUT_UI_RESOLVE_RESULT_V2_SIZE_64,
        struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        load_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        resolve_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        resolve_flags: 0,
        reserved: [0; 3],
    }
}

const fn empty_music_volume_result() -> WinStickerMusicVolumeResultV1 {
    WinStickerMusicVolumeResultV1 {
        struct_size: WINSTICKER_MUSIC_VOLUME_RESULT_V1_SIZE_64,
        struct_version: WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        match_kind: WINSTICKER_MUSIC_VOLUME_MATCH_NONE,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        device_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        system_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        session_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        has_session_volume: 0,
        operation_succeeded: 0,
        reserved0: 0,
        system_volume: 0.0,
        session_volume: 0.0,
        reserved: [0; 4],
    }
}

const fn empty_explorer_shell_launch_result() -> WinStickerExplorerShellLaunchResultV1 {
    WinStickerExplorerShellLaunchResultV1 {
        struct_size: WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64,
        struct_version: WINSTICKER_EXPLORER_SHELL_LAUNCH_STRUCT_VERSION_1,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        windows_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        desktop_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        document_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        application_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        execute_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        operation_succeeded: 0,
        reserved0: 0,
        reserved: [0; 4],
    }
}

const fn empty_quick_access_result() -> WinStickerQuickAccessResultV1 {
    WinStickerQuickAccessResultV1 {
        struct_size: WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64,
        struct_version: WINSTICKER_QUICK_ACCESS_STRUCT_VERSION_1,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        quick_namespace_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        items_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        enumerate_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        item_path_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        property_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        parent_namespace_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        parse_name_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        invoke_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        pin_state: WINSTICKER_QUICK_ACCESS_PIN_STATE_UNKNOWN,
        operation_succeeded: 0,
        matched_item: 0,
        fallback_used: 0,
        reserved0: 0,
        reserved: [0; 4],
    }
}

const fn empty_recycle_bin_result() -> WinStickerRecycleBinResultV1 {
    WinStickerRecycleBinResultV1 {
        struct_size: WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64,
        struct_version: WINSTICKER_RECYCLE_BIN_STRUCT_VERSION_1,
        status: WINSTICKER_NATIVE_STATUS_OK,
        operation_hresult: WINSTICKER_NATIVE_S_OK,
        attempted_phases: 0,
        com_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        create_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        namespace_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        items_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        enumerate_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        item_name_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        property_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        invoke_hresult: WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED,
        matched_count: 0,
        restored_count: 0,
        operation_succeeded: 0,
        reserved0: 0,
        reserved1: 0,
        reserved: [0; 4],
    }
}

fn is_valid_output_buffer(buffer: &WinStickerNativeUtf16BufferV1) -> bool {
    buffer.reserved0 == 0
        && ((buffer.capacity_chars == 0 && buffer.data.is_null())
            || (buffer.capacity_chars > 0 && !buffer.data.is_null()))
}

fn is_valid_input_string(value: &WinStickerNativeUtf16StringV1, allow_empty: bool) -> bool {
    value.reserved0 == 0
        && value.length_chars <= WINSTICKER_SHORTCUT_MAX_INPUT_VALUE_CHARS
        && if value.length_chars == 0 {
            allow_empty && value.data.is_null()
        } else {
            !value.data.is_null()
        }
}

unsafe fn is_valid_music_input_string(value: &WinStickerNativeUtf16StringV1) -> bool {
    if value.reserved0 != 0 || value.length_chars > WINSTICKER_MUSIC_VOLUME_MAX_INPUT_CHARS {
        return false;
    }

    if value.length_chars == 0 {
        return value.data.is_null();
    }

    if value.data.is_null() {
        return false;
    }

    // SAFETY: Pointer readability for the declared length is an ABI caller contract.
    let value_slice =
        unsafe { std::slice::from_raw_parts(value.data, value.length_chars as usize) };
    !value_slice.contains(&0)
}

unsafe fn validate_result_envelope(result: *mut WinStickerShortcutReadResultV2) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, writable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerShortcutReadResultV2>() as u32
        || result_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_read_request(request: *const WinStickerShortcutReadRequestV2) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerShortcutReadRequestV2>() as u32
        || request_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    if request_ref.flags != 0
        || request_ref.reserved0 != 0
        || request_ref.reserved != [0; 4]
        || request_ref.shortcut_path.is_null()
        || request_ref.shortcut_path_length_chars == 0
        || request_ref.shortcut_path_length_chars > WINSTICKER_SHORTCUT_MAX_INPUT_PATH_CHARS
        || !matches!(
            request_ref.mode,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW | WINSTICKER_SHORTCUT_READ_MODE_EFFECTIVE_DIAGNOSTIC
        )
        || !is_valid_output_buffer(&request_ref.target_path)
        || !is_valid_output_buffer(&request_ref.description)
        || !is_valid_output_buffer(&request_ref.arguments)
        || !is_valid_output_buffer(&request_ref.working_directory)
        || !is_valid_output_buffer(&request_ref.icon_path)
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_write_result_envelope(result: *mut WinStickerShortcutWriteResultV2) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, writable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerShortcutWriteResultV2>() as u32
        || result_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_write_request(request: *const WinStickerShortcutWriteRequestV2) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerShortcutWriteRequestV2>() as u32
        || request_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    if request_ref.flags & !WINSTICKER_SHORTCUT_WRITE_FLAG_SHELL_NAMESPACE_TARGET != 0
        || request_ref.reserved != [0; 4]
        || !is_valid_input_string(&request_ref.shortcut_path, false)
        || !is_valid_input_string(&request_ref.target_path, false)
        || !is_valid_input_string(&request_ref.description, true)
        || !is_valid_input_string(&request_ref.arguments, true)
        || !is_valid_input_string(&request_ref.working_directory, true)
        || !is_valid_input_string(&request_ref.icon_path, true)
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_ui_resolve_result_envelope(
    result: *mut WinStickerShortcutUiResolveResultV2,
) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, writable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerShortcutUiResolveResultV2>() as u32
        || result_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_ui_resolve_request(request: *const WinStickerShortcutUiResolveRequestV2) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerShortcutUiResolveRequestV2>() as u32
        || request_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    if request_ref.flags != 0
        || request_ref.reserved0 != 0
        || request_ref.reserved != [0; 3]
        || !is_valid_input_string(&request_ref.shortcut_path, false)
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_music_volume_result_envelope(result: *mut WinStickerMusicVolumeResultV1) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerMusicVolumeResultV1>() as u32
        || result_ref.struct_version != WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_music_volume_request(request: *const WinStickerMusicVolumeRequestV1) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerMusicVolumeRequestV1>() as u32
        || request_ref.struct_version != WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    if request_ref.flags != 0
        || request_ref.reserved != [0; 4]
        || !matches!(
            request_ref.operation,
            WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SNAPSHOT
                | WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SYSTEM
                | WINSTICKER_MUSIC_VOLUME_OPERATION_SET_SYSTEM
                | WINSTICKER_MUSIC_VOLUME_OPERATION_SET_SESSION
        )
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_music_input_string(&request_ref.source_app_user_model_id) }
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_music_input_string(&request_ref.source_display_name) }
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn is_valid_explorer_shell_input_string(
    value: &WinStickerNativeUtf16StringV1,
    allow_empty: bool,
) -> bool {
    if value.reserved0 != 0 || value.length_chars > WINSTICKER_EXPLORER_SHELL_LAUNCH_MAX_INPUT_CHARS {
        return false;
    }

    if value.length_chars == 0 {
        return allow_empty && value.data.is_null();
    }

    if value.data.is_null() {
        return false;
    }

    // SAFETY: Pointer readability for the declared length is part of the ABI contract.
    let input = unsafe { std::slice::from_raw_parts(value.data, value.length_chars as usize) };
    !input.contains(&0)
}

unsafe fn validate_explorer_shell_launch_result_envelope(
    result: *mut WinStickerExplorerShellLaunchResultV1,
) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerExplorerShellLaunchResultV1>() as u32
        || result_ref.struct_version != WINSTICKER_EXPLORER_SHELL_LAUNCH_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_explorer_shell_launch_request(
    request: *const WinStickerExplorerShellLaunchRequestV1,
) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerExplorerShellLaunchRequestV1>() as u32
        || request_ref.struct_version != WINSTICKER_EXPLORER_SHELL_LAUNCH_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    if request_ref.flags != 0
        || request_ref.reserved0 != 0
        || request_ref.reserved != [0; 4]
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_explorer_shell_input_string(&request_ref.path, false) }
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe {
            is_valid_explorer_shell_input_string(&request_ref.working_directory, true)
        }
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_explorer_shell_input_string(&request_ref.verb, false) }
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn is_valid_quick_access_input_string(
    value: &WinStickerNativeUtf16StringV1,
    allow_empty: bool,
) -> bool {
    if value.reserved0 != 0 || value.length_chars > WINSTICKER_QUICK_ACCESS_MAX_INPUT_CHARS {
        return false;
    }

    if value.length_chars == 0 {
        return allow_empty && value.data.is_null();
    }

    if value.data.is_null() {
        return false;
    }

    // SAFETY: Pointer readability for the declared length is part of the ABI contract.
    let input = unsafe { std::slice::from_raw_parts(value.data, value.length_chars as usize) };
    !input.contains(&0)
}

unsafe fn validate_quick_access_result_envelope(result: *mut WinStickerQuickAccessResultV1) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerQuickAccessResultV1>() as u32
        || result_ref.struct_version != WINSTICKER_QUICK_ACCESS_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_quick_access_request(request: *const WinStickerQuickAccessRequestV1) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerQuickAccessRequestV1>() as u32
        || request_ref.struct_version != WINSTICKER_QUICK_ACCESS_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    let is_query = request_ref.operation == WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE;
    if request_ref.flags != 0
        || request_ref.reserved != [0; 4]
        || !matches!(
            request_ref.operation,
            WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE
                | WINSTICKER_QUICK_ACCESS_OPERATION_PIN
                | WINSTICKER_QUICK_ACCESS_OPERATION_UNPIN
        )
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_quick_access_input_string(&request_ref.folder_path, false) }
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_quick_access_input_string(&request_ref.parent_path, is_query) }
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_quick_access_input_string(&request_ref.folder_name, is_query) }
        || (is_query
            && (request_ref.parent_path.length_chars != 0
                || request_ref.folder_name.length_chars != 0))
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn is_valid_recycle_bin_input_string(value: &WinStickerNativeUtf16StringV1) -> bool {
    if value.reserved0 != 0
        || value.length_chars == 0
        || value.length_chars > WINSTICKER_RECYCLE_BIN_MAX_INPUT_CHARS
        || value.data.is_null()
    {
        return false;
    }

    // SAFETY: Pointer readability for the declared length is part of the ABI contract.
    let input = unsafe { std::slice::from_raw_parts(value.data, value.length_chars as usize) };
    !input.contains(&0)
}

unsafe fn validate_recycle_bin_result_envelope(result: *mut WinStickerRecycleBinResultV1) -> u32 {
    if result.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable result pointer.
    let result_ref = unsafe { &*result };
    if result_ref.struct_size != size_of::<WinStickerRecycleBinResultV1>() as u32
        || result_ref.struct_version != WINSTICKER_RECYCLE_BIN_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn validate_recycle_bin_request(request: *const WinStickerRecycleBinRequestV1) -> u32 {
    if request.is_null() {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: The exported ABI requires a valid, readable request pointer.
    let request_ref = unsafe { &*request };
    if request_ref.struct_size != size_of::<WinStickerRecycleBinRequestV1>() as u32
        || request_ref.struct_version != WINSTICKER_RECYCLE_BIN_STRUCT_VERSION_1
    {
        return WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT;
    }

    if request_ref.flags != 0
        || request_ref.reserved != [0; 4]
        || !matches!(
            request_ref.operation,
            WINSTICKER_RECYCLE_BIN_OPERATION_QUERY | WINSTICKER_RECYCLE_BIN_OPERATION_RESTORE
        )
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_recycle_bin_input_string(&request_ref.original_parent) }
        // SAFETY: Request pointer readability is an ABI caller contract.
        || !unsafe { is_valid_recycle_bin_input_string(&request_ref.original_name) }
    {
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    WINSTICKER_NATIVE_STATUS_OK
}

unsafe fn initialize_valid_result(result: *mut WinStickerShortcutReadResultV2) {
    // SAFETY: The caller first passed validate_result_envelope.
    unsafe { result.write(empty_result()) };
}

unsafe fn write_failure(
    result: *mut WinStickerShortcutReadResultV2,
    status: u32,
    operation_hresult: i32,
) {
    // SAFETY: The caller first passed validate_result_envelope and initialized the result.
    let result_ref = unsafe { &mut *result };
    result_ref.status = status;
    result_ref.operation_hresult = operation_hresult;
}

unsafe fn set_write_failure(
    result: *mut WinStickerShortcutWriteResultV2,
    status: u32,
    operation_hresult: i32,
) {
    // SAFETY: The caller first validated and initialized the write result.
    let result_ref = unsafe { &mut *result };
    result_ref.status = status;
    result_ref.operation_hresult = operation_hresult;
}

unsafe fn set_ui_resolve_failure(
    result: *mut WinStickerShortcutUiResolveResultV2,
    status: u32,
    operation_hresult: i32,
) {
    // SAFETY: The caller first validated and initialized the UI resolve result.
    let result_ref = unsafe { &mut *result };
    result_ref.status = status;
    result_ref.operation_hresult = operation_hresult;
}

/// Returns the ABI version implemented by this native module.
#[unsafe(no_mangle)]
pub extern "C" fn winsticker_native_abi_version() -> u32 {
    WINSTICKER_NATIVE_ABI_VERSION
}

/// Returns the operation capabilities that are safe to call in this module.
#[unsafe(no_mangle)]
pub extern "C" fn winsticker_native_capabilities() -> u64 {
    WINSTICKER_NATIVE_CAPABILITIES
}

/// Reads shortcut metadata using one of the ABI 2 compatibility modes.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerShortcutReadRequestV2` and `result`
/// must point to a readable and writable `WinStickerShortcutReadResultV2` for the
/// duration of the call. Any non-null output buffer must be writable for its
/// declared capacity.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_shortcut_read_v2(
    request: *const WinStickerShortcutReadRequestV2,
    result: *mut WinStickerShortcutReadResultV2,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { initialize_valid_result(result) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_read_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        unsafe { write_failure(result, request_status, WINSTICKER_NATIVE_E_INVALIDARG) };
        return request_status;
    }

    // SAFETY: Both envelopes and all declared pointers passed ABI validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Pointer lifetimes and writable output capacities are guaranteed by the ABI caller.
    unsafe { shortcut::read_shortcut(request_ref, result_ref) }
}

/// Resolves a shortcut without UI and then reads its stored metadata.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerShortcutResolveRequestV2` and
/// `result` must point to a readable and writable
/// `WinStickerShortcutReadResultV2` for the duration of the call. Any non-null
/// output buffer must be writable for its declared capacity.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_shortcut_resolve_no_ui_v2(
    request: *const WinStickerShortcutResolveRequestV2,
    result: *mut WinStickerShortcutReadResultV2,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { initialize_valid_result(result) };
    if request.is_null() {
        // SAFETY: The result envelope was validated and initialized above.
        unsafe {
            write_failure(
                result,
                WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT,
                WINSTICKER_NATIVE_E_INVALIDARG,
            )
        };
        return WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT;
    }

    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_ref = unsafe { &*request };
    let request_status = if request_ref.struct_size
        != size_of::<WinStickerShortcutResolveRequestV2>() as u32
        || request_ref.struct_version != WINSTICKER_NATIVE_STRUCT_VERSION_2
    {
        WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT
    } else if request_ref.flags != 0
        || request_ref.timeout_ms > u16::MAX as u32
        || request_ref.reserved != [0; 4]
    {
        WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT
    } else {
        // SAFETY: read_request is an embedded, readable request value.
        let read_status = unsafe { validate_read_request(&request_ref.read_request) };
        if read_status == WINSTICKER_NATIVE_STATUS_OK
            && request_ref.read_request.mode != WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW
        {
            WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT
        } else {
            read_status
        }
    };

    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        unsafe { write_failure(result, request_status, WINSTICKER_NATIVE_E_INVALIDARG) };
        return request_status;
    }

    // SAFETY: Both envelopes and all declared pointers passed ABI validation.
    let read_request = &request_ref.read_request;
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Pointer lifetimes and writable output capacities are guaranteed by the ABI caller.
    unsafe { shortcut::resolve_shortcut(read_request, result_ref, request_ref.timeout_ms) }
}

/// Creates or overwrites a shortcut with the complete supplied metadata.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerShortcutWriteRequestV2`; every
/// non-null UTF-16 pointer must remain readable for its declared length.
/// `result` must remain readable and writable for the duration of the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_shortcut_write_v2(
    request: *const WinStickerShortcutWriteRequestV2,
    result: *mut WinStickerShortcutWriteResultV2,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_write_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { result.write(empty_write_result()) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_write_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        unsafe { set_write_failure(result, request_status, WINSTICKER_NATIVE_E_INVALIDARG) };
        return request_status;
    }

    // SAFETY: Both envelopes and all declared pointers passed ABI validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Input pointer lifetimes are guaranteed by the ABI caller.
    unsafe { shortcut::write_shortcut(request_ref, result_ref) }
}

/// Lets Windows repair a shortcut with its standard UI and delete offer.
///
/// The call is synchronous and runs on the caller's thread. `owner_hwnd` is
/// forwarded unchanged as the parent for any Shell dialog.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerShortcutUiResolveRequestV2`; its
/// path pointer must remain readable for the declared length. `result` must
/// remain readable and writable for the duration of the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_shortcut_resolve_with_ui_v2(
    request: *const WinStickerShortcutUiResolveRequestV2,
    result: *mut WinStickerShortcutUiResolveResultV2,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_ui_resolve_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { result.write(empty_ui_resolve_result()) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_ui_resolve_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        unsafe { set_ui_resolve_failure(result, request_status, WINSTICKER_NATIVE_E_INVALIDARG) };
        return request_status;
    }

    // SAFETY: Both envelopes and the input path passed ABI validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Input pointer lifetime is guaranteed by the ABI caller.
    unsafe { shortcut::resolve_shortcut_with_ui(request_ref, result_ref) }
}

/// Gets or sets default-render and application-session volume through Core Audio.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerMusicVolumeRequestV1`; non-null
/// UTF-16 pointers must remain readable for their declared lengths. `result`
/// must remain readable and writable for the duration of the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_music_volume_v1(
    request: *const WinStickerMusicVolumeRequestV1,
    result: *mut WinStickerMusicVolumeResultV1,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_music_volume_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { result.write(empty_music_volume_result()) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_music_volume_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        let result_ref = unsafe { &mut *result };
        result_ref.status = request_status;
        result_ref.operation_hresult = WINSTICKER_NATIVE_E_INVALIDARG;
        return request_status;
    }

    // SAFETY: Both envelopes and all declared input pointers passed validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Input pointer lifetimes are guaranteed by the ABI caller.
    unsafe { music_volume::execute(request_ref, result_ref) }
}

/// Launches a file or URI through the Shell object hosted by the running
/// Explorer desktop process so the child inherits Explorer's environment.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerExplorerShellLaunchRequestV1`;
/// non-null UTF-16 pointers must remain readable for their declared lengths.
/// `result` must remain readable and writable for the duration of the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_explorer_shell_launch_v1(
    request: *const WinStickerExplorerShellLaunchRequestV1,
    result: *mut WinStickerExplorerShellLaunchResultV1,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_explorer_shell_launch_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { result.write(empty_explorer_shell_launch_result()) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_explorer_shell_launch_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        let result_ref = unsafe { &mut *result };
        result_ref.status = request_status;
        result_ref.operation_hresult = WINSTICKER_NATIVE_E_INVALIDARG;
        return request_status;
    }

    // SAFETY: Both envelopes and all declared input pointers passed validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Input pointer lifetimes are guaranteed by the ABI caller.
    unsafe { explorer_shell_launch::execute(request_ref, result_ref) }
}

/// Queries, pins, or unpins a File Explorer Quick Access folder.
///
/// The call is synchronous and uses the caller's thread. WinSticker routes public
/// asynchronous operations through a dedicated STA thread before entering this
/// boundary.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerQuickAccessRequestV1`; non-null
/// UTF-16 pointers must remain readable for their declared lengths. `result`
/// must remain readable and writable for the duration of the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_quick_access_v1(
    request: *const WinStickerQuickAccessRequestV1,
    result: *mut WinStickerQuickAccessResultV1,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_quick_access_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { result.write(empty_quick_access_result()) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_quick_access_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        let result_ref = unsafe { &mut *result };
        result_ref.status = request_status;
        result_ref.operation_hresult = WINSTICKER_NATIVE_E_INVALIDARG;
        return request_status;
    }

    // SAFETY: Both envelopes and all declared input pointers passed validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Input pointer lifetimes are guaranteed by the ABI caller.
    unsafe { quick_access::execute(request_ref, result_ref) }
}

/// Queries or restores one exact Recycle Bin item identified by its original
/// parent directory and original display name.
///
/// The call is synchronous and uses the caller's thread. It never empties the
/// Recycle Bin and never invokes a verb for a non-matching item.
///
/// # Safety
///
/// `request` must point to a readable `WinStickerRecycleBinRequestV1`; non-null
/// UTF-16 pointers must remain readable for their declared lengths. `result`
/// must remain readable and writable for the duration of the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn winsticker_recycle_bin_v1(
    request: *const WinStickerRecycleBinRequestV1,
    result: *mut WinStickerRecycleBinResultV1,
) -> u32 {
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let result_status = unsafe { validate_recycle_bin_result_envelope(result) };
    if result_status != WINSTICKER_NATIVE_STATUS_OK {
        return result_status;
    }

    // SAFETY: The result envelope was validated above.
    unsafe { result.write(empty_recycle_bin_result()) };
    // SAFETY: Pointer validity is part of the exported ABI's caller contract.
    let request_status = unsafe { validate_recycle_bin_request(request) };
    if request_status != WINSTICKER_NATIVE_STATUS_OK {
        // SAFETY: The result envelope was validated and initialized above.
        let result_ref = unsafe { &mut *result };
        result_ref.status = request_status;
        result_ref.operation_hresult = WINSTICKER_NATIVE_E_INVALIDARG;
        return request_status;
    }

    // SAFETY: Both envelopes and all declared input pointers passed validation.
    let request_ref = unsafe { &*request };
    // SAFETY: The result envelope was validated and initialized above.
    let result_ref = unsafe { &mut *result };
    // SAFETY: Input pointer lifetimes are guaranteed by the ABI caller.
    unsafe { recycle_bin::execute(request_ref, result_ref) }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        fs,
        os::windows::ffi::OsStrExt,
        path::{Path, PathBuf},
        ptr::{null, null_mut},
        sync::atomic::{AtomicU64, Ordering},
        thread,
    };
    use windows::{
        Win32::{
            Foundation::RPC_E_CHANGED_MODE,
            System::Com::{
                CLSCTX_INPROC_SERVER, COINIT, COINIT_APARTMENTTHREADED, COINIT_MULTITHREADED,
                CoCreateInstance, CoInitializeEx, CoUninitialize, IPersistFile,
            },
            UI::Shell::{IShellLinkW, ShellLink},
        },
        core::{Interface, PCWSTR},
    };

    static NEXT_FIXTURE_ID: AtomicU64 = AtomicU64::new(1);

    const ALL_SHORTCUT_FIELDS: u32 = WINSTICKER_SHORTCUT_FIELD_TARGET_PATH
        | WINSTICKER_SHORTCUT_FIELD_DESCRIPTION
        | WINSTICKER_SHORTCUT_FIELD_ARGUMENTS
        | WINSTICKER_SHORTCUT_FIELD_WORKING_DIRECTORY
        | WINSTICKER_SHORTCUT_FIELD_ICON_PATH;
    const READ_PHASES: u32 = WINSTICKER_SHORTCUT_PHASE_COM_INITIALIZE
        | WINSTICKER_SHORTCUT_PHASE_CREATE_OBJECT
        | WINSTICKER_SHORTCUT_PHASE_LOAD;
    const RESOLVE_PHASES: u32 = READ_PHASES | WINSTICKER_SHORTCUT_PHASE_RESOLVE;
    const WRITE_PHASES: u32 = WINSTICKER_SHORTCUT_PHASE_COM_INITIALIZE
        | WINSTICKER_SHORTCUT_PHASE_CREATE_OBJECT
        | WINSTICKER_SHORTCUT_PHASE_SAVE;

    struct TestComGuard {
        active: bool,
    }

    impl Drop for TestComGuard {
        fn drop(&mut self) {
            if self.active {
                // SAFETY: The guard owns one successful CoInitializeEx call.
                unsafe { CoUninitialize() };
            }
        }
    }

    struct ShortcutFixture {
        root: PathBuf,
        link_path: PathBuf,
        target_path: PathBuf,
        working_directory: PathBuf,
        icon_path: PathBuf,
        description: String,
        arguments: String,
        icon_index: i32,
        include_optional_fields: bool,
    }

    impl ShortcutFixture {
        fn new(arguments: &str) -> Self {
            Self::new_with_optional_fields(arguments, true)
        }

        fn minimal() -> Self {
            Self::new_with_optional_fields("", false)
        }

        fn new_with_optional_fields(arguments: &str, include_optional_fields: bool) -> Self {
            let id = NEXT_FIXTURE_ID.fetch_add(1, Ordering::Relaxed);
            let root = std::env::temp_dir().join(format!(
                "winsticker-native-shortcut-{}-{id}",
                std::process::id()
            ));
            fs::create_dir_all(&root).expect("create shortcut fixture root");

            let target_path = root.join("目标 application.exe");
            let working_directory = root.join("工作 目录");
            let icon_path = root.join("图标 source.ico");
            let link_path = root.join("测试 shortcut.lnk");
            fs::write(&target_path, b"fixture").expect("create shortcut target");
            fs::create_dir_all(&working_directory).expect("create shortcut working directory");
            fs::write(&icon_path, b"icon").expect("create shortcut icon");

            let fixture = Self {
                root,
                link_path,
                target_path,
                working_directory,
                icon_path,
                description: "WinSticker 描述 Δ".to_string(),
                arguments: arguments.to_string(),
                icon_index: -7,
                include_optional_fields,
            };
            fixture.write_link();
            fixture
        }

        fn write_link(&self) {
            let _com_guard = initialize_com(COINIT_MULTITHREADED);
            // SAFETY: The CLSID and interface are generated by windows-rs.
            let shell_link: IShellLinkW = unsafe {
                CoCreateInstance(&ShellLink, None, CLSCTX_INPROC_SERVER)
                    .expect("create test shell link")
            };
            let target = nul_terminated_path(&self.target_path);
            let description = nul_terminated_text(&self.description);
            let arguments = nul_terminated_text(&self.arguments);
            let working_directory = nul_terminated_path(&self.working_directory);
            let icon_path = nul_terminated_path(&self.icon_path);

            // SAFETY: Every UTF-16 value is NUL-terminated and alive for the call.
            unsafe {
                shell_link
                    .SetPath(PCWSTR(target.as_ptr()))
                    .expect("set shortcut target");
                if self.include_optional_fields {
                    shell_link
                        .SetDescription(PCWSTR(description.as_ptr()))
                        .expect("set shortcut description");
                    shell_link
                        .SetArguments(PCWSTR(arguments.as_ptr()))
                        .expect("set shortcut arguments");
                    shell_link
                        .SetWorkingDirectory(PCWSTR(working_directory.as_ptr()))
                        .expect("set shortcut working directory");
                    shell_link
                        .SetIconLocation(PCWSTR(icon_path.as_ptr()), self.icon_index)
                        .expect("set shortcut icon");
                }
            }

            let persist_file: IPersistFile = shell_link.cast().expect("cast test IPersistFile");
            let link_path = nul_terminated_path(&self.link_path);
            // SAFETY: The link path is NUL-terminated and alive for the call.
            unsafe {
                persist_file
                    .Save(PCWSTR(link_path.as_ptr()), true)
                    .expect("save test shell link");
            }
        }
    }

    impl Drop for ShortcutFixture {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.root);
        }
    }

    struct ReadCall {
        status: u32,
        result: WinStickerShortcutReadResultV2,
        target: Vec<u16>,
        description: Vec<u16>,
        arguments: Vec<u16>,
        working_directory: Vec<u16>,
        icon: Vec<u16>,
    }

    struct WriteValues<'a> {
        target: &'a str,
        description: &'a str,
        arguments: &'a str,
        working_directory: &'a str,
        icon: &'a str,
        icon_index: i32,
    }

    impl ReadCall {
        fn target_text(&self) -> String {
            decoded_output(&self.target, self.result.target_required_chars)
        }

        fn description_text(&self) -> String {
            decoded_output(&self.description, self.result.description_required_chars)
        }

        fn arguments_text(&self) -> String {
            decoded_output(&self.arguments, self.result.arguments_required_chars)
        }

        fn working_directory_text(&self) -> String {
            decoded_output(
                &self.working_directory,
                self.result.working_directory_required_chars,
            )
        }

        fn icon_text(&self) -> String {
            decoded_output(&self.icon, self.result.icon_required_chars)
        }
    }

    fn initialize_com(mode: COINIT) -> TestComGuard {
        // SAFETY: The returned guard balances every successful initialization.
        let hresult = unsafe { CoInitializeEx(None, mode) };
        assert!(
            hresult.0 >= 0 || hresult.0 == RPC_E_CHANGED_MODE.0,
            "CoInitializeEx failed with 0x{:08X}",
            hresult.0 as u32
        );
        TestComGuard {
            active: hresult.0 >= 0,
        }
    }

    fn nul_terminated_path(path: &Path) -> Vec<u16> {
        path.as_os_str().encode_wide().chain([0]).collect()
    }

    fn nul_terminated_text(value: &str) -> Vec<u16> {
        value.encode_utf16().chain([0]).collect()
    }

    fn output_buffer(storage: &mut [u16]) -> WinStickerNativeUtf16BufferV1 {
        WinStickerNativeUtf16BufferV1 {
            data: if storage.is_empty() {
                null_mut()
            } else {
                storage.as_mut_ptr()
            },
            capacity_chars: storage.len() as u32,
            reserved0: 0,
        }
    }

    fn input_string(storage: &[u16]) -> WinStickerNativeUtf16StringV1 {
        WinStickerNativeUtf16StringV1 {
            data: if storage.is_empty() {
                null()
            } else {
                storage.as_ptr()
            },
            length_chars: storage.len() as u32,
            reserved0: 0,
        }
    }

    fn read_shortcut_file(
        path: &Path,
        mode: u32,
        capacities: [usize; 5],
        initial_value: u16,
    ) -> ReadCall {
        let path_utf16: Vec<u16> = path.as_os_str().encode_wide().collect();
        let mut target = vec![initial_value; capacities[0]];
        let mut description = vec![initial_value; capacities[1]];
        let mut arguments = vec![initial_value; capacities[2]];
        let mut working_directory = vec![initial_value; capacities[3]];
        let mut icon = vec![initial_value; capacities[4]];
        let request = WinStickerShortcutReadRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            mode,
            flags: 0,
            shortcut_path: path_utf16.as_ptr(),
            shortcut_path_length_chars: path_utf16.len() as u32,
            reserved0: 0,
            target_path: output_buffer(&mut target),
            description: output_buffer(&mut description),
            arguments: output_buffer(&mut arguments),
            working_directory: output_buffer(&mut working_directory),
            icon_path: output_buffer(&mut icon),
            reserved: [0; 4],
        };
        let mut result = empty_result();
        // SAFETY: All request pointers and output buffers remain valid for the call.
        let status = unsafe { winsticker_shortcut_read_v2(&request, &mut result) };
        ReadCall {
            status,
            result,
            target,
            description,
            arguments,
            working_directory,
            icon,
        }
    }

    fn resolve_shortcut_file(
        path: &Path,
        timeout_ms: u32,
        capacities: [usize; 5],
        initial_value: u16,
    ) -> ReadCall {
        let path_utf16: Vec<u16> = path.as_os_str().encode_wide().collect();
        let mut target = vec![initial_value; capacities[0]];
        let mut description = vec![initial_value; capacities[1]];
        let mut arguments = vec![initial_value; capacities[2]];
        let mut working_directory = vec![initial_value; capacities[3]];
        let mut icon = vec![initial_value; capacities[4]];
        let read_request = WinStickerShortcutReadRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            mode: WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            flags: 0,
            shortcut_path: path_utf16.as_ptr(),
            shortcut_path_length_chars: path_utf16.len() as u32,
            reserved0: 0,
            target_path: output_buffer(&mut target),
            description: output_buffer(&mut description),
            arguments: output_buffer(&mut arguments),
            working_directory: output_buffer(&mut working_directory),
            icon_path: output_buffer(&mut icon),
            reserved: [0; 4],
        };
        let request = WinStickerShortcutResolveRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            timeout_ms,
            flags: 0,
            read_request,
            reserved: [0; 4],
        };
        let mut result = empty_result();
        // SAFETY: All request pointers and output buffers remain valid for the call.
        let status = unsafe { winsticker_shortcut_resolve_no_ui_v2(&request, &mut result) };
        ReadCall {
            status,
            result,
            target,
            description,
            arguments,
            working_directory,
            icon,
        }
    }

    fn resolve_shortcut_with_ui_file(
        path: &Path,
        owner_hwnd: u64,
    ) -> (u32, WinStickerShortcutUiResolveResultV2) {
        let path_utf16: Vec<u16> = path.as_os_str().encode_wide().collect();
        let request = WinStickerShortcutUiResolveRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            flags: 0,
            reserved0: 0,
            shortcut_path: input_string(&path_utf16),
            owner_hwnd,
            reserved: [0; 3],
        };
        let mut result = empty_ui_resolve_result();
        // SAFETY: The path storage and result remain valid for this synchronous call.
        let status = unsafe { winsticker_shortcut_resolve_with_ui_v2(&request, &mut result) };
        (status, result)
    }

    fn write_shortcut_file(
        path: &Path,
        values: &WriteValues<'_>,
    ) -> (u32, WinStickerShortcutWriteResultV2) {
        let shortcut_path: Vec<u16> = path.as_os_str().encode_wide().collect();
        let target: Vec<u16> = values.target.encode_utf16().collect();
        let description: Vec<u16> = values.description.encode_utf16().collect();
        let arguments: Vec<u16> = values.arguments.encode_utf16().collect();
        let working_directory: Vec<u16> = values.working_directory.encode_utf16().collect();
        let icon: Vec<u16> = values.icon.encode_utf16().collect();
        let request = WinStickerShortcutWriteRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            flags: 0,
            icon_index: values.icon_index,
            shortcut_path: input_string(&shortcut_path),
            target_path: input_string(&target),
            description: input_string(&description),
            arguments: input_string(&arguments),
            working_directory: input_string(&working_directory),
            icon_path: input_string(&icon),
            reserved: [0; 4],
        };
        let mut result = empty_write_result();
        // SAFETY: Every request slice and the result remain valid for the call.
        let status = unsafe { winsticker_shortcut_write_v2(&request, &mut result) };
        (status, result)
    }

    fn decoded_output(buffer: &[u16], required_chars: u32) -> String {
        assert!(required_chars > 0);
        assert!(buffer.len() >= required_chars as usize);
        String::from_utf16(&buffer[..required_chars as usize - 1])
            .expect("shortcut output must be valid UTF-16")
    }

    fn query_buffer() -> WinStickerNativeUtf16BufferV1 {
        WinStickerNativeUtf16BufferV1 {
            data: null_mut(),
            capacity_chars: 0,
            reserved0: 0,
        }
    }

    fn valid_read_request(path: &[u16]) -> WinStickerShortcutReadRequestV2 {
        WinStickerShortcutReadRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            mode: WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            flags: 0,
            shortcut_path: path.as_ptr(),
            shortcut_path_length_chars: path.len() as u32,
            reserved0: 0,
            target_path: query_buffer(),
            description: query_buffer(),
            arguments: query_buffer(),
            working_directory: query_buffer(),
            icon_path: query_buffer(),
            reserved: [0; 4],
        }
    }

    fn valid_explorer_shell_launch_request(
        path: &[u16],
        working_directory: &[u16],
        verb: &[u16],
    ) -> WinStickerExplorerShellLaunchRequestV1 {
        WinStickerExplorerShellLaunchRequestV1 {
            struct_size: WINSTICKER_EXPLORER_SHELL_LAUNCH_REQUEST_V1_SIZE_64,
            struct_version: WINSTICKER_EXPLORER_SHELL_LAUNCH_STRUCT_VERSION_1,
            flags: 0,
            reserved0: 0,
            path: input_string(path),
            working_directory: input_string(working_directory),
            verb: input_string(verb),
            reserved: [0; 4],
        }
    }

    fn valid_quick_access_request(
        operation: u32,
        folder_path: &[u16],
        parent_path: &[u16],
        folder_name: &[u16],
    ) -> WinStickerQuickAccessRequestV1 {
        WinStickerQuickAccessRequestV1 {
            struct_size: WINSTICKER_QUICK_ACCESS_REQUEST_V1_SIZE_64,
            struct_version: WINSTICKER_QUICK_ACCESS_STRUCT_VERSION_1,
            operation,
            flags: 0,
            folder_path: input_string(folder_path),
            parent_path: input_string(parent_path),
            folder_name: input_string(folder_name),
            reserved: [0; 4],
        }
    }

    fn valid_recycle_bin_request(
        operation: u32,
        original_parent: &[u16],
        original_name: &[u16],
    ) -> WinStickerRecycleBinRequestV1 {
        WinStickerRecycleBinRequestV1 {
            struct_size: WINSTICKER_RECYCLE_BIN_REQUEST_V1_SIZE_64,
            struct_version: WINSTICKER_RECYCLE_BIN_STRUCT_VERSION_1,
            operation,
            flags: 0,
            original_parent: input_string(original_parent),
            original_name: input_string(original_name),
            reserved: [0; 4],
        }
    }

    #[test]
    fn exported_contract_matches_abi_v2() {
        assert_eq!(winsticker_native_abi_version(), 2);
        assert_eq!(winsticker_native_capabilities(), 511);
    }

    #[test]
    fn x64_struct_layout_is_frozen() {
        assert_eq!(size_of::<WinStickerNativeUtf16BufferV1>(), 16);
        assert_eq!(
            size_of::<WinStickerNativeUtf16StringV1>(),
            WINSTICKER_NATIVE_UTF16_STRING_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutReadRequestV2>(),
            WINSTICKER_SHORTCUT_READ_REQUEST_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutReadResultV2>(),
            WINSTICKER_SHORTCUT_READ_RESULT_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutResolveRequestV2>(),
            WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutWriteRequestV2>(),
            WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutWriteResultV2>(),
            WINSTICKER_SHORTCUT_WRITE_RESULT_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutUiResolveRequestV2>(),
            WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerShortcutUiResolveResultV2>(),
            WINSTICKER_SHORTCUT_UI_RESOLVE_RESULT_V2_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerMusicVolumeRequestV1>(),
            WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerMusicVolumeResultV1>(),
            WINSTICKER_MUSIC_VOLUME_RESULT_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerExplorerShellLaunchRequestV1>(),
            WINSTICKER_EXPLORER_SHELL_LAUNCH_REQUEST_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerExplorerShellLaunchResultV1>(),
            WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerQuickAccessRequestV1>(),
            WINSTICKER_QUICK_ACCESS_REQUEST_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerQuickAccessResultV1>(),
            WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerRecycleBinRequestV1>(),
            WINSTICKER_RECYCLE_BIN_REQUEST_V1_SIZE_64 as usize
        );
        assert_eq!(
            size_of::<WinStickerRecycleBinResultV1>(),
            WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64 as usize
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinRequestV1, original_parent),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinRequestV1, original_name),
            32
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinRequestV1, reserved),
            48
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinResultV1, attempted_phases),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinResultV1, matched_count),
            52
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinResultV1, operation_succeeded),
            60
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerRecycleBinResultV1, reserved),
            72
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessRequestV1, folder_path),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessRequestV1, parent_path),
            32
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessRequestV1, folder_name),
            48
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessRequestV1, reserved),
            64
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessResultV1, attempted_phases),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessResultV1, pin_state),
            60
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessResultV1, operation_succeeded),
            64
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerQuickAccessResultV1, reserved),
            80
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchRequestV1, path),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchRequestV1, working_directory),
            32
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchRequestV1, verb),
            48
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchRequestV1, reserved),
            64
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchResultV1, attempted_phases),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchResultV1, operation_succeeded),
            48
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerExplorerShellLaunchResultV1, reserved),
            56
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeRequestV1, source_app_user_model_id),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeRequestV1, source_display_name),
            32
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeRequestV1, volume),
            48
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeRequestV1, reserved),
            56
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeResultV1, attempted_phases),
            16
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeResultV1, com_hresult),
            24
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeResultV1, has_session_volume),
            44
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeResultV1, system_volume),
            56
        );
        assert_eq!(
            std::mem::offset_of!(WinStickerMusicVolumeResultV1, reserved),
            72
        );
    }

    #[test]
    fn music_volume_rejects_invalid_operation_before_com_is_attempted() {
        let request = WinStickerMusicVolumeRequestV1 {
            struct_size: WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64,
            struct_version: WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1,
            operation: u32::MAX,
            flags: 0,
            source_app_user_model_id: input_string(&[]),
            source_display_name: input_string(&[]),
            volume: 0.0,
            reserved: [0; 4],
        };
        let mut result = empty_music_volume_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_music_volume_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn music_volume_rejects_embedded_nul_before_com_is_attempted() {
        let source_app = [b'a' as u16, 0, b'b' as u16];
        let request = WinStickerMusicVolumeRequestV1 {
            struct_size: WINSTICKER_MUSIC_VOLUME_REQUEST_V1_SIZE_64,
            struct_version: WINSTICKER_MUSIC_VOLUME_STRUCT_VERSION_1,
            operation: WINSTICKER_MUSIC_VOLUME_OPERATION_GET_SNAPSHOT,
            flags: 0,
            source_app_user_model_id: input_string(&source_app),
            source_display_name: input_string(&[]),
            volume: 0.0,
            reserved: [0; 4],
        };
        let mut result = empty_music_volume_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_music_volume_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn explorer_shell_launch_rejects_empty_required_inputs_before_com_is_attempted() {
        let verb: Vec<u16> = "open".encode_utf16().collect();
        let request = valid_explorer_shell_launch_request(&[], &[], &verb);
        let mut result = empty_explorer_shell_launch_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_explorer_shell_launch_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.operation_succeeded, 0);
    }

    #[test]
    fn explorer_shell_launch_rejects_embedded_nul_before_com_is_attempted() {
        let path = [b'a' as u16, 0, b'b' as u16];
        let verb: Vec<u16> = "open".encode_utf16().collect();
        let request = valid_explorer_shell_launch_request(&path, &[], &verb);
        let mut result = empty_explorer_shell_launch_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_explorer_shell_launch_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn explorer_shell_launch_rejects_incompatible_result_without_touching_it() {
        let path: Vec<u16> = "https://winsticker.fun".encode_utf16().collect();
        let verb: Vec<u16> = "open".encode_utf16().collect();
        let request = valid_explorer_shell_launch_request(&path, &[], &verb);
        let mut result = empty_explorer_shell_launch_result();
        result.struct_size -= 8;

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_explorer_shell_launch_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT);
        assert_eq!(
            result.struct_size,
            WINSTICKER_EXPLORER_SHELL_LAUNCH_RESULT_V1_SIZE_64 - 8
        );
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn quick_access_rejects_invalid_operation_before_com_is_attempted() {
        let folder: Vec<u16> = r"C:\WinSticker".encode_utf16().collect();
        let request = valid_quick_access_request(u32::MAX, &folder, &[], &[]);
        let mut result = empty_quick_access_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_quick_access_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.operation_succeeded, 0);
    }

    #[test]
    fn quick_access_query_rejects_parent_item_inputs_before_com_is_attempted() {
        let folder: Vec<u16> = r"C:\WinSticker".encode_utf16().collect();
        let parent: Vec<u16> = r"C:\".encode_utf16().collect();
        let request = valid_quick_access_request(
            WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE,
            &folder,
            &parent,
            &[],
        );
        let mut result = empty_quick_access_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_quick_access_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn quick_access_rejects_embedded_nul_before_com_is_attempted() {
        let folder = [b'C' as u16, b':' as u16, b'\\' as u16, 0, b'x' as u16];
        let request = valid_quick_access_request(
            WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE,
            &folder,
            &[],
            &[],
        );
        let mut result = empty_quick_access_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_quick_access_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn quick_access_rejects_incompatible_result_without_touching_it() {
        let folder: Vec<u16> = r"C:\WinSticker".encode_utf16().collect();
        let request = valid_quick_access_request(
            WINSTICKER_QUICK_ACCESS_OPERATION_QUERY_PIN_STATE,
            &folder,
            &[],
            &[],
        );
        let mut result = empty_quick_access_result();
        result.struct_size -= 8;

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_quick_access_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT);
        assert_eq!(
            result.struct_size,
            WINSTICKER_QUICK_ACCESS_RESULT_V1_SIZE_64 - 8
        );
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn recycle_bin_rejects_invalid_operation_before_com_is_attempted() {
        let parent: Vec<u16> = r"C:\WinSticker".encode_utf16().collect();
        let name: Vec<u16> = "owned-item".encode_utf16().collect();
        let request = valid_recycle_bin_request(u32::MAX, &parent, &name);
        let mut result = empty_recycle_bin_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_recycle_bin_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.operation_succeeded, 0);
    }

    #[test]
    fn recycle_bin_rejects_empty_or_embedded_nul_identity_before_com() {
        let parent: Vec<u16> = r"C:\WinSticker".encode_utf16().collect();
        let embedded_nul = [b'o' as u16, 0, b'x' as u16];
        let empty_request =
            valid_recycle_bin_request(WINSTICKER_RECYCLE_BIN_OPERATION_QUERY, &parent, &[]);
        let nul_request =
            valid_recycle_bin_request(WINSTICKER_RECYCLE_BIN_OPERATION_QUERY, &parent, &embedded_nul);

        for request in [empty_request, nul_request] {
            let mut result = empty_recycle_bin_result();
            // SAFETY: The test supplies valid request and result structure memory.
            let status = unsafe { winsticker_recycle_bin_v1(&request, &mut result) };
            assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
            assert_eq!(result.attempted_phases, 0);
        }
    }

    #[test]
    fn recycle_bin_rejects_incompatible_result_without_touching_it() {
        let parent: Vec<u16> = r"C:\WinSticker".encode_utf16().collect();
        let name: Vec<u16> = "owned-item".encode_utf16().collect();
        let request =
            valid_recycle_bin_request(WINSTICKER_RECYCLE_BIN_OPERATION_QUERY, &parent, &name);
        let mut result = empty_recycle_bin_result();
        result.struct_size -= 8;

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_recycle_bin_v1(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT);
        assert_eq!(
            result.struct_size,
            WINSTICKER_RECYCLE_BIN_RESULT_V1_SIZE_64 - 8
        );
        assert_eq!(result.attempted_phases, 0);
    }

    #[test]
    fn write_creates_real_unicode_shortcut_with_all_fields() {
        let fixture = ShortcutFixture::new("--old");
        fs::remove_file(&fixture.link_path).expect("remove original shortcut");
        let target = fixture.target_path.to_string_lossy();
        let working_directory = fixture.working_directory.to_string_lossy();
        let icon = fixture.icon_path.to_string_lossy();
        let values = WriteValues {
            target: &target,
            description: "Rust 写入描述 🚀",
            arguments: "  --write=\"值\"  ",
            working_directory: &working_directory,
            icon: &icon,
            icon_index: -11,
        };

        let (status, result) = write_shortcut_file(&fixture.link_path, &values);

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(result.attempted_phases, WRITE_PHASES);
        assert_eq!(result.attempted_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(result.save_hresult, WINSTICKER_NATIVE_S_OK);
        let read = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [520; 5],
            0,
        );
        assert_eq!(read.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(read.target_text(), target);
        assert_eq!(read.description_text(), values.description);
        assert_eq!(read.arguments_text(), values.arguments);
        assert_eq!(read.working_directory_text(), working_directory);
        assert_eq!(read.icon_text(), icon);
        assert_eq!(read.result.icon_index, values.icon_index);
    }

    #[test]
    fn write_overwrites_existing_shortcut_and_clears_optional_fields() {
        let fixture = ShortcutFixture::new("--old-arguments");
        let replacement_target = fixture.root.join("replacement target.exe");
        fs::write(&replacement_target, b"replacement").expect("create replacement target");
        let replacement_target_text = replacement_target.to_string_lossy();
        let values = WriteValues {
            target: &replacement_target_text,
            description: "",
            arguments: "",
            working_directory: "",
            icon: "",
            icon_index: 0,
        };

        let (status, result) = write_shortcut_file(&fixture.link_path, &values);

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        let read = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [520; 5],
            0,
        );
        assert_eq!(read.target_text(), replacement_target_text);
        assert_eq!(read.description_text(), "");
        assert_eq!(read.arguments_text(), "");
        assert_eq!(read.working_directory_text(), "");
        assert_eq!(read.icon_text(), "");
        assert_eq!(read.result.icon_index, 0);
    }

    #[test]
    fn write_rejects_embedded_nul_before_com_is_attempted() {
        let shortcut_path = [b'x' as u16];
        let target = [b't' as u16];
        let description = [b'a' as u16, 0, b'b' as u16];
        let request = WinStickerShortcutWriteRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            flags: 0,
            icon_index: 0,
            shortcut_path: input_string(&shortcut_path),
            target_path: input_string(&target),
            description: input_string(&description),
            arguments: input_string(&[]),
            working_directory: input_string(&[]),
            icon_path: input_string(&[]),
            reserved: [0; 4],
        };
        let mut result = empty_write_result();

        // SAFETY: The test supplies valid request and result structures.
        let status = unsafe { winsticker_shortcut_write_v2(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.attempted_fields, 0);
    }

    #[test]
    fn write_rejects_empty_required_and_oversized_values_before_com() {
        let shortcut_path = [b'x' as u16];
        let target = [b't' as u16];
        let empty_target_request = WinStickerShortcutWriteRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_WRITE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            flags: 0,
            icon_index: 0,
            shortcut_path: input_string(&shortcut_path),
            target_path: input_string(&[]),
            description: input_string(&[]),
            arguments: input_string(&[]),
            working_directory: input_string(&[]),
            icon_path: input_string(&[]),
            reserved: [0; 4],
        };
        let mut empty_target_result = empty_write_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let empty_target_status =
            unsafe { winsticker_shortcut_write_v2(&empty_target_request, &mut empty_target_result) };

        assert_eq!(empty_target_status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(empty_target_result.attempted_phases, 0);

        let oversized_description = vec![b'x' as u16; 32_768];
        let oversized_request = WinStickerShortcutWriteRequestV2 {
            target_path: input_string(&target),
            description: input_string(&oversized_description),
            ..empty_target_request
        };
        let mut oversized_result = empty_write_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let oversized_status =
            unsafe { winsticker_shortcut_write_v2(&oversized_request, &mut oversized_result) };

        assert_eq!(oversized_status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(oversized_result.attempted_phases, 0);
    }

    #[test]
    fn write_reports_save_failure_after_all_setters() {
        let fixture = ShortcutFixture::new("--save-failure");
        let missing_parent_link = fixture.root.join("missing-parent").join("write.lnk");
        let target = fixture.target_path.to_string_lossy();
        let values = WriteValues {
            target: &target,
            description: "save failure",
            arguments: "--save-failure",
            working_directory: "",
            icon: "",
            icon_index: 0,
        };

        let (status, result) = write_shortcut_file(&missing_parent_link, &values);

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_OPERATION_FAILED);
        assert_eq!(result.attempted_phases, WRITE_PHASES);
        assert_eq!(result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        assert_ne!(result.save_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(result.operation_hresult, result.save_hresult);
        assert!(!missing_parent_link.exists());
    }

    #[test]
    fn write_reuses_existing_sta_apartment() {
        thread::spawn(|| {
            let fixture = ShortcutFixture::new("--write-sta");
            let _outer_com_guard = initialize_com(COINIT_APARTMENTTHREADED);
            let target = fixture.target_path.to_string_lossy();
            let values = WriteValues {
                target: &target,
                description: "STA",
                arguments: "--write-sta",
                working_directory: "",
                icon: "",
                icon_index: 0,
            };

            let (status, result) = write_shortcut_file(&fixture.link_path, &values);

            assert_eq!(status, WINSTICKER_NATIVE_STATUS_OK);
            assert_eq!(result.com_hresult, RPC_E_CHANGED_MODE.0);
            assert_eq!(result.attempted_phases, WRITE_PHASES);
        })
        .join()
        .expect("STA shortcut write thread");
    }

    #[test]
    fn read_rejects_embedded_nul_before_com_is_attempted() {
        let path = [b'x' as u16, 0, b'y' as u16];
        let request = valid_read_request(&path);
        let mut result = empty_result();

        // SAFETY: The test supplies valid request and result structures.
        let status = unsafe { winsticker_shortcut_read_v2(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.attempted_fields, 0);
    }

    #[test]
    fn read_reports_invalid_request_when_result_is_compatible() {
        let mut result = empty_result();

        // SAFETY: The null request is intentional and the result is valid.
        let status = unsafe { winsticker_shortcut_read_v2(null(), &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
    }

    #[test]
    fn incompatible_result_is_left_untouched() {
        let path = [b'x' as u16];
        let request = valid_read_request(&path);
        let mut result = empty_result();
        result.struct_size = 0;
        result.status = 0xFFFF_FFFF;

        // SAFETY: The deliberately incompatible result still points to valid memory.
        let status = unsafe { winsticker_shortcut_read_v2(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INCOMPATIBLE_STRUCT);
        assert_eq!(result.struct_size, 0);
        assert_eq!(result.status, 0xFFFF_FFFF);
    }

    #[test]
    fn resolve_flags_encode_only_no_ui_no_search_and_timeout() {
        assert_eq!(shortcut::resolve_flags(0), 0x0000_0011);
        assert_eq!(shortcut::resolve_flags(1), 0x0001_0011);
        assert_eq!(shortcut::resolve_flags(u16::MAX as u32), 0xFFFF_0011);
    }

    #[test]
    fn resolve_with_ui_flags_match_legacy_product_contract() {
        assert_eq!(shortcut::resolve_with_ui_flags(), 0x0000_0214);
        assert_eq!(shortcut::resolve_with_ui_flags() & 0x0000_0001, 0);
    }

    #[test]
    fn resolve_with_ui_accepts_owner_and_records_all_phases_for_valid_link() {
        let fixture = ShortcutFixture::new("--resolve-with-ui");
        let (status, result) = resolve_shortcut_with_ui_file(&fixture.link_path, 0);

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(result.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(result.attempted_phases, RESOLVE_PHASES);
        assert!(result.resolve_hresult >= 0);
        assert_eq!(result.resolve_flags, 0x0000_0214);
        assert!(fixture.link_path.exists());
    }

    #[test]
    fn resolve_with_ui_rejects_embedded_nul_before_com_is_attempted() {
        let path = [b'x' as u16, 0, b'y' as u16];
        let request = WinStickerShortcutUiResolveRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_UI_RESOLVE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            flags: 0,
            reserved0: 0,
            shortcut_path: input_string(&path),
            owner_hwnd: 0,
            reserved: [0; 3],
        };
        let mut result = empty_ui_resolve_result();

        // SAFETY: The test supplies valid request and result structure memory.
        let status = unsafe { winsticker_shortcut_resolve_with_ui_v2(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.resolve_flags, 0);
    }

    #[test]
    fn resolve_with_ui_reports_corrupt_shortcut_before_resolve() {
        let fixture = ShortcutFixture::new("--corrupt-ui");
        fs::write(&fixture.link_path, b"not a shell link").expect("corrupt UI shortcut fixture");
        let (status, result) = resolve_shortcut_with_ui_file(&fixture.link_path, 0);

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_LOAD_FAILED);
        assert_eq!(
            result.attempted_phases,
            WINSTICKER_SHORTCUT_PHASE_COM_INITIALIZE
                | WINSTICKER_SHORTCUT_PHASE_CREATE_OBJECT
                | WINSTICKER_SHORTCUT_PHASE_LOAD
        );
        assert!(result.load_hresult < 0);
        assert_eq!(result.operation_hresult, result.load_hresult);
        assert_eq!(result.resolve_hresult, WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED);
        assert_eq!(result.resolve_flags, 0x0000_0214);
    }

    #[test]
    fn resolve_rejects_timeout_above_u16_before_com_is_attempted() {
        let path = [b'x' as u16];
        let request = WinStickerShortcutResolveRequestV2 {
            struct_size: WINSTICKER_SHORTCUT_RESOLVE_REQUEST_V2_SIZE_64,
            struct_version: WINSTICKER_NATIVE_STRUCT_VERSION_2,
            timeout_ms: u16::MAX as u32 + 1,
            flags: 0,
            read_request: valid_read_request(&path),
            reserved: [0; 4],
        };
        let mut result = empty_result();

        // SAFETY: The test supplies valid request and result structures.
        let status = unsafe { winsticker_shortcut_resolve_no_ui_v2(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.attempted_fields, 0);
    }

    #[test]
    fn resolve_no_ui_reads_real_metadata_and_records_resolve_phase() {
        let fixture = ShortcutFixture::new("--resolve=\u{503c}");
        let call = resolve_shortcut_file(&fixture.link_path, 100, [520; 5], 0);

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(call.result.operation_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(call.result.attempted_phases, RESOLVE_PHASES);
        assert!(call.result.resolve_hresult >= 0);
        assert_eq!(call.result.attempted_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(PathBuf::from(call.target_text()), fixture.target_path);
        assert_eq!(call.description_text(), fixture.description);
        assert_eq!(call.arguments_text(), fixture.arguments);
        assert_eq!(
            PathBuf::from(call.working_directory_text()),
            fixture.working_directory
        );
        assert_eq!(PathBuf::from(call.icon_text()), fixture.icon_path);
        assert_eq!(call.result.icon_index, fixture.icon_index);
    }

    #[test]
    fn unresolved_target_still_returns_loaded_stored_metadata() {
        let fixture = ShortcutFixture::new("--missing-resolve-target");
        fs::remove_file(&fixture.target_path).expect("remove shortcut target");
        let call = resolve_shortcut_file(&fixture.link_path, 1, [520; 5], 0);

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(call.result.operation_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(call.result.attempted_phases, RESOLVE_PHASES);
        assert_ne!(call.result.resolve_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(PathBuf::from(call.target_text()), fixture.target_path);
        assert_eq!(call.arguments_text(), fixture.arguments);
    }

    #[test]
    fn resolve_no_ui_reuses_existing_sta_apartment() {
        thread::spawn(|| {
            let fixture = ShortcutFixture::new("--resolve-sta");
            let _outer_com_guard = initialize_com(COINIT_APARTMENTTHREADED);
            let call = resolve_shortcut_file(&fixture.link_path, 100, [520; 5], 0);

            assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
            assert_eq!(call.result.com_hresult, RPC_E_CHANGED_MODE.0);
            assert_eq!(call.result.attempted_phases, RESOLVE_PHASES);
        })
        .join()
        .expect("STA shortcut resolve thread");
    }

    #[test]
    fn stored_raw_reads_real_unicode_shortcut_without_trimming() {
        let fixture = ShortcutFixture::new("  --alpha=\"值\"  ");
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [520; 5],
            0,
        );

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(call.result.operation_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(call.result.attempted_phases, READ_PHASES);
        assert_eq!(call.result.create_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(call.result.load_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(
            call.result.resolve_hresult,
            WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED
        );

        assert_eq!(call.result.attempted_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.present_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.caller_buffer_too_small_fields, 0);
        assert_eq!(call.result.source_truncated_fields, 0);

        assert_eq!(PathBuf::from(call.target_text()), fixture.target_path);
        assert_eq!(call.description_text(), fixture.description);
        assert_eq!(call.arguments_text(), fixture.arguments);
        assert_eq!(
            PathBuf::from(call.working_directory_text()),
            fixture.working_directory
        );
        assert_eq!(PathBuf::from(call.icon_text()), fixture.icon_path);
        assert_eq!(call.result.icon_index, fixture.icon_index);
    }

    #[test]
    fn effective_diagnostic_reads_only_target_and_trimmed_arguments() {
        let fixture = ShortcutFixture::new("\u{2003}  --alpha=\"\u{503c}\"  \u{3000}");
        let sentinel = 0x55AA;
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_EFFECTIVE_DIAGNOSTIC,
            [520, 4, 600, 4, 4],
            sentinel,
        );

        let diagnostic_fields =
            WINSTICKER_SHORTCUT_FIELD_TARGET_PATH | WINSTICKER_SHORTCUT_FIELD_ARGUMENTS;
        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(call.result.operation_hresult, WINSTICKER_NATIVE_S_OK);
        assert_eq!(call.result.attempted_phases, READ_PHASES);
        assert_eq!(call.result.attempted_fields, diagnostic_fields);
        assert_eq!(call.result.succeeded_fields, diagnostic_fields);
        assert_eq!(call.result.present_fields, diagnostic_fields);
        assert_eq!(call.result.caller_buffer_too_small_fields, 0);
        assert_eq!(call.result.source_truncated_fields, 0);
        assert_eq!(PathBuf::from(call.target_text()), fixture.target_path);
        assert_eq!(call.arguments_text(), "--alpha=\"\u{503c}\"");

        assert_eq!(
            call.result.description_hresult,
            WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED
        );
        assert_eq!(
            call.result.working_directory_hresult,
            WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED
        );
        assert_eq!(
            call.result.icon_hresult,
            WINSTICKER_NATIVE_HRESULT_NOT_ATTEMPTED
        );
        assert_eq!(call.result.description_required_chars, 0);
        assert_eq!(call.result.working_directory_required_chars, 0);
        assert_eq!(call.result.icon_required_chars, 0);
        assert!(call.description.iter().all(|value| *value == sentinel));
        assert!(
            call.working_directory
                .iter()
                .all(|value| *value == sentinel)
        );
        assert!(call.icon.iter().all(|value| *value == sentinel));
    }

    #[test]
    fn stored_raw_query_reports_exact_required_lengths_for_every_field() {
        let fixture = ShortcutFixture::new("--query=\u{503c}");
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [0; 5],
            0,
        );

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_BUFFER_TOO_SMALL);
        assert_eq!(
            call.result.operation_hresult,
            WINSTICKER_NATIVE_HRESULT_INSUFFICIENT_BUFFER
        );
        assert_eq!(call.result.attempted_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.present_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(
            call.result.caller_buffer_too_small_fields,
            ALL_SHORTCUT_FIELDS
        );
        assert_eq!(call.result.source_truncated_fields, 0);
        assert_eq!(
            call.result.target_required_chars,
            fixture.target_path.as_os_str().encode_wide().count() as u32 + 1
        );
        assert_eq!(
            call.result.description_required_chars,
            fixture.description.encode_utf16().count() as u32 + 1
        );
        assert_eq!(
            call.result.arguments_required_chars,
            fixture.arguments.encode_utf16().count() as u32 + 1
        );
        assert_eq!(
            call.result.working_directory_required_chars,
            fixture.working_directory.as_os_str().encode_wide().count() as u32 + 1
        );
        assert_eq!(
            call.result.icon_required_chars,
            fixture.icon_path.as_os_str().encode_wide().count() as u32 + 1
        );
    }

    #[test]
    fn too_small_caller_buffer_is_not_partially_written() {
        let fixture = ShortcutFixture::new("--small-buffer");
        let sentinel = 0x55AA;
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [2, 520, 520, 520, 520],
            sentinel,
        );

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_BUFFER_TOO_SMALL);
        assert_eq!(
            call.result.caller_buffer_too_small_fields,
            WINSTICKER_SHORTCUT_FIELD_TARGET_PATH
        );
        assert_eq!(call.target, vec![sentinel; 2]);
        assert_eq!(call.description_text(), fixture.description);
        assert_eq!(call.arguments_text(), fixture.arguments);
        assert_eq!(
            PathBuf::from(call.working_directory_text()),
            fixture.working_directory
        );
        assert_eq!(PathBuf::from(call.icon_text()), fixture.icon_path);
    }

    #[test]
    fn corrupted_shortcut_reports_load_failure_before_field_reads() {
        let fixture = ShortcutFixture::new("--corrupt");
        fs::write(&fixture.link_path, b"not a shell link").expect("corrupt shortcut fixture");
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [520; 5],
            0,
        );

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_LOAD_FAILED);
        assert_eq!(call.result.attempted_phases, READ_PHASES);
        assert_eq!(call.result.create_hresult, WINSTICKER_NATIVE_S_OK);
        assert!(call.result.load_hresult < 0);
        assert_eq!(call.result.operation_hresult, call.result.load_hresult);
        assert_eq!(call.result.attempted_fields, 0);
        assert_eq!(call.result.succeeded_fields, 0);
        assert_eq!(call.result.present_fields, 0);
    }

    #[test]
    fn absent_optional_fields_are_successful_empty_values() {
        let fixture = ShortcutFixture::minimal();
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [520; 5],
            0x55AA,
        );

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(call.result.attempted_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(call.result.succeeded_fields, ALL_SHORTCUT_FIELDS);
        assert_eq!(
            call.result.present_fields,
            WINSTICKER_SHORTCUT_FIELD_TARGET_PATH
        );
        assert_eq!(PathBuf::from(call.target_text()), fixture.target_path);
        assert_eq!(call.description_text(), "");
        assert_eq!(call.arguments_text(), "");
        assert_eq!(call.working_directory_text(), "");
        assert_eq!(call.icon_text(), "");
        assert_eq!(call.result.description_required_chars, 1);
        assert_eq!(call.result.arguments_required_chars, 1);
        assert_eq!(call.result.working_directory_required_chars, 1);
        assert_eq!(call.result.icon_required_chars, 1);
        assert_eq!(call.result.icon_index, 0);
    }

    #[test]
    fn stored_raw_preserves_target_when_target_file_is_missing() {
        let fixture = ShortcutFixture::new("--missing-target");
        fs::remove_file(&fixture.target_path).expect("remove shortcut target");
        let call = read_shortcut_file(
            &fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [520; 5],
            0,
        );

        assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(PathBuf::from(call.target_text()), fixture.target_path);
        assert_eq!(call.result.target_hresult, WINSTICKER_NATIVE_S_OK);
    }

    #[test]
    fn nested_mta_call_records_s_false_and_balances_its_com_count() {
        thread::spawn(|| {
            let fixture = ShortcutFixture::new("--mta");
            let _outer_com_guard = initialize_com(COINIT_MULTITHREADED);
            let call = read_shortcut_file(
                &fixture.link_path,
                WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
                [520; 5],
                0,
            );

            assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
            assert_eq!(call.result.com_hresult, WINSTICKER_NATIVE_S_FALSE);
        })
        .join()
        .expect("MTA shortcut read thread");
    }

    #[test]
    fn existing_sta_call_records_changed_mode_and_reuses_the_apartment() {
        thread::spawn(|| {
            let fixture = ShortcutFixture::new("--sta");
            let _outer_com_guard = initialize_com(COINIT_APARTMENTTHREADED);
            let call = read_shortcut_file(
                &fixture.link_path,
                WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
                [520; 5],
                0,
            );

            assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK);
            assert_eq!(call.result.com_hresult, RPC_E_CHANGED_MODE.0);
        })
        .join()
        .expect("STA shortcut read thread");
    }

    #[test]
    fn stored_raw_argument_source_boundary_is_reported_conservatively() {
        for length in [259, 260, 261] {
            let fixture = ShortcutFixture::new(&"x".repeat(length));
            let call = read_shortcut_file(
                &fixture.link_path,
                WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
                [600; 5],
                0,
            );

            assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK, "length {length}");
            assert_eq!(call.arguments_text(), "x".repeat(length.min(259)));
            assert_eq!(
                call.result.source_truncated_fields, WINSTICKER_SHORTCUT_FIELD_ARGUMENTS,
                "length {length}"
            );
        }
    }

    #[test]
    fn diagnostic_argument_source_boundary_is_reported_conservatively() {
        for length in [511, 512, 513] {
            let fixture = ShortcutFixture::new(&"y".repeat(length));
            let call = read_shortcut_file(
                &fixture.link_path,
                WINSTICKER_SHORTCUT_READ_MODE_EFFECTIVE_DIAGNOSTIC,
                [600; 5],
                0,
            );

            assert_eq!(call.status, WINSTICKER_NATIVE_STATUS_OK, "length {length}");
            assert_eq!(call.arguments_text(), "y".repeat(length.min(511)));
            assert_eq!(
                call.result.source_truncated_fields, WINSTICKER_SHORTCUT_FIELD_ARGUMENTS,
                "length {length}"
            );
        }
    }

    #[test]
    fn long_unicode_arguments_are_preserved_in_both_read_modes() {
        let stored_arguments = "\u{53c2}\u{6570}\u{1f980}".repeat(60);
        let stored_fixture = ShortcutFixture::new(&stored_arguments);
        let stored_call = read_shortcut_file(
            &stored_fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_STORED_RAW,
            [600; 5],
            0,
        );

        assert_eq!(stored_call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(stored_call.arguments_text(), stored_arguments);
        assert_eq!(stored_call.result.source_truncated_fields, 0);

        let diagnostic_value = "\u{8def}\u{5f84}\u{1f680}".repeat(110);
        let diagnostic_arguments = format!("\u{3000}{diagnostic_value}\u{2003}");
        let diagnostic_fixture = ShortcutFixture::new(&diagnostic_arguments);
        let diagnostic_call = read_shortcut_file(
            &diagnostic_fixture.link_path,
            WINSTICKER_SHORTCUT_READ_MODE_EFFECTIVE_DIAGNOSTIC,
            [600; 5],
            0,
        );

        assert_eq!(diagnostic_call.status, WINSTICKER_NATIVE_STATUS_OK);
        assert_eq!(diagnostic_call.arguments_text(), diagnostic_value);
        assert_eq!(diagnostic_call.result.source_truncated_fields, 0);
    }

    #[test]
    fn oversized_input_path_is_rejected_before_com_is_attempted() {
        let path = vec![b'x' as u16; WINSTICKER_SHORTCUT_MAX_INPUT_PATH_CHARS as usize + 1];
        let request = valid_read_request(&path);
        let mut result = empty_result();

        // SAFETY: The test supplies valid request and result structures.
        let status = unsafe { winsticker_shortcut_read_v2(&request, &mut result) };

        assert_eq!(status, WINSTICKER_NATIVE_STATUS_INVALID_ARGUMENT);
        assert_eq!(result.operation_hresult, WINSTICKER_NATIVE_E_INVALIDARG);
        assert_eq!(result.attempted_phases, 0);
        assert_eq!(result.attempted_fields, 0);
    }
}
