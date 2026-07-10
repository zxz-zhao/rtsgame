cmake_minimum_required(VERSION 3.20)

project(UnityRTSNativeServer VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(unity_rts_server_cpp
    src/main.cpp
    src/app_config.cpp
    src/runtime_utils.cpp
    src/async_logger.cpp
    src/alert_reporter.cpp
    src/battle_relay_server.cpp
    src/native_http_server.cpp
    src/server_crypto.cpp
    src/server_json_utils.cpp
    src/server_mysql_store.cpp
    src/server_redis_store.cpp
    src/server_storage.cpp
    src/server_service.cpp
    src/server_security.cpp
    third_party/crypt_blowfish/crypt_blowfish.c
    third_party/crypt_blowfish/crypt_gensalt.c
)

add_executable(unity_rts_guardian_cpp
    src/guardian_main.cpp
    src/app_config.cpp
    src/runtime_utils.cpp
    src/async_logger.cpp
    src/alert_reporter.cpp
)

target_include_directories(unity_rts_server_cpp
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
        ${CMAKE_CURRENT_SOURCE_DIR}/third_party
        ${CMAKE_CURRENT_SOURCE_DIR}/third_party/crypt_blowfish
)

target_include_directories(unity_rts_guardian_cpp
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_compile_definitions(unity_rts_server_cpp
    PRIVATE
        UNITY_RTS_SERVER_VERSION="${PROJECT_VERSION}"
)

target_compile_definitions(unity_rts_guardian_cpp
    PRIVATE
        UNITY_RTS_SERVER_VERSION="${PROJECT_VERSION}"
)

if(MSVC)
    target_compile_options(unity_rts_server_cpp PRIVATE /W4 /permissive- /utf-8)
    target_compile_options(unity_rts_guardian_cpp PRIVATE /W4 /permissive- /utf-8)
else()
    target_compile_options(unity_rts_server_cpp PRIVATE -Wall -Wextra -Wpedantic)
    target_compile_options(unity_rts_guardian_cpp PRIVATE -Wall -Wextra -Wpedantic)
    target_compile_options(unity_rts_server_cpp PRIVATE $<$<COMPILE_LANGUAGE:C>:-U__i386__>)
    target_link_libraries(unity_rts_server_cpp PRIVATE dl)
endif()

if(WIN32)
    target_compile_definitions(unity_rts_server_cpp PRIVATE UNITY_RTS_SERVER_PLATFORM="windows")
    target_compile_definitions(unity_rts_guardian_cpp PRIVATE UNITY_RTS_SERVER_PLATFORM="windows")
    target_link_libraries(unity_rts_server_cpp PRIVATE ws2_32)
    target_link_libraries(unity_rts_guardian_cpp PRIVATE ws2_32)
else()
    target_compile_definitions(unity_rts_server_cpp PRIVATE UNITY_RTS_SERVER_PLATFORM="linux")
    target_compile_definitions(unity_rts_guardian_cpp PRIVATE UNITY_RTS_SERVER_PLATFORM="linux")
endif()

install(TARGETS unity_rts_server_cpp unity_rts_guardian_cpp
    RUNTIME DESTINATION bin
)

install(FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/config/server.env.example
    DESTINATION .
    RENAME .env.example
)

install(FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/README.md
    ${CMAKE_CURRENT_SOURCE_DIR}/DEPLOY.md
    ${CMAKE_CURRENT_SOURCE_DIR}/MIGRATION.md
    DESTINATION .
)

if(DEFINED MYSQL_CLIENT_RUNTIME_DLL AND EXISTS "${MYSQL_CLIENT_RUNTIME_DLL}")
    install(FILES
        "${MYSQL_CLIENT_RUNTIME_DLL}"
        DESTINATION bin
    )
endif()

install(DIRECTORY
    ${CMAKE_CURRENT_SOURCE_DIR}/runtime/
    DESTINATION .
)

install(DIRECTORY
    ${CMAKE_CURRENT_SOURCE_DIR}/scripts/
    DESTINATION scripts
    FILE_PERMISSIONS
        OWNER_READ OWNER_WRITE OWNER_EXECUTE
        GROUP_READ GROUP_EXECUTE
        WORLD_READ WORLD_EXECUTE
)

set(CPACK_PACKAGE_NAME "unity-rts-server-cpp")
set(CPACK_PACKAGE_VENDOR "OpenAI Codex")
set(CPACK_PACKAGE_VERSION ${PROJECT_VERSION})
set(CPACK_PACKAGE_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/dist)

set(UNITY_RTS_PACKAGE_ARCH "${CMAKE_SYSTEM_PROCESSOR}")
if(NOT UNITY_RTS_PACKAGE_ARCH)
    if(CMAKE_VS_PLATFORM_NAME)
        set(UNITY_RTS_PACKAGE_ARCH "${CMAKE_VS_PLATFORM_NAME}")
    elseif(CMAKE_GENERATOR_PLATFORM)
        set(UNITY_RTS_PACKAGE_ARCH "${CMAKE_GENERATOR_PLATFORM}")
    elseif(WIN32)
        set(UNITY_RTS_PACKAGE_ARCH "x64")
    else()
        set(UNITY_RTS_PACKAGE_ARCH "${CMAKE_HOST_SYSTEM_PROCESSOR}")
    endif()
endif()

set(CPACK_PACKAGE_FILE_NAME "unity-rts-server-cpp-${PROJECT_VERSION}-${CMAKE_SYSTEM_NAME}-${UNITY_RTS_PACKAGE_ARCH}")

if(WIN32)
    set(CPACK_GENERATOR "ZIP")
else()
    set(CPACK_GENERATOR "TGZ")
endif()

include(CPack)
