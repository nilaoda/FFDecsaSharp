#include <stdint.h>
#include <stdio.h>
#include <string.h>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <time.h>
#endif

#include "FFdecsa.h"

#define BATCH_SIZE 128
#define PACKET_SIZE 188
#define PAYLOAD_SIZE 184
#define WARMUP_BATCHES 5000
#define MEASUREMENT_BATCHES 30000

static unsigned char source[PACKET_SIZE * BATCH_SIZE];
static unsigned char packets[PACKET_SIZE * BATCH_SIZE];
static unsigned char *cluster[BATCH_SIZE + 2];

static void create_source_packets(void) {
  int packet_index;
  int payload_index;

  for (packet_index = 0; packet_index < BATCH_SIZE; packet_index++) {
    unsigned char *packet = source + (packet_index * PACKET_SIZE);
    packet[0] = 0x47;
    packet[3] = 0xd0;
    for (payload_index = 0; payload_index < PAYLOAD_SIZE; payload_index++) {
      packet[payload_index + 4] = (unsigned char)((packet_index * 29) + (payload_index * 37));
    }
  }
}

#ifdef _WIN32
static LARGE_INTEGER timer_frequency;

static uint64_t elapsed_nanoseconds(const LARGE_INTEGER *start, const LARGE_INTEGER *end) {
  return (uint64_t)(((end->QuadPart - start->QuadPart) * UINT64_C(1000000000)) / timer_frequency.QuadPart);
}
#else
static uint64_t elapsed_nanoseconds(const struct timespec *start, const struct timespec *end) {
  return ((uint64_t)(end->tv_sec - start->tv_sec) * UINT64_C(1000000000))
      + (uint64_t)(end->tv_nsec - start->tv_nsec);
}
#endif

static uint64_t compute_fnv1a64(const unsigned char *data, size_t length) {
  size_t index;
  uint64_t hash = UINT64_C(14695981039346656037);

  for (index = 0; index < length; index++) {
    hash = (hash ^ data[index]) * UINT64_C(1099511628211);
  }

  return hash;
}

static void prepare_cluster(void) {
  cluster[0] = packets;
  cluster[1] = packets + sizeof(packets);
  cluster[2] = NULL;
}

int main(void) {
  static const unsigned char even[8] = {0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00};
  static const unsigned char odd[8] = {0x0f, 0x1e, 0x2d, 0x3c, 0x4b, 0x5a, 0x69, 0x78};
  void *keys = get_key_struct();
#ifdef _WIN32
  LARGE_INTEGER start;
  LARGE_INTEGER end;
  if (!QueryPerformanceFrequency(&timer_frequency)) {
    free_key_struct(keys);
    return 6;
  }
#else
  struct timespec start;
  struct timespec end;
#endif
  uint64_t total_nanoseconds = 0;
  uint64_t expected_hash;
  uint64_t actual_hash;
  int iteration;

  if (keys == NULL) {
    return 1;
  }

  create_source_packets();
  set_control_words(keys, even, odd);

  memcpy(packets, source, sizeof(packets));
  prepare_cluster();
  if (decrypt_packets(keys, cluster) != BATCH_SIZE) {
    free_key_struct(keys);
    return 2;
  }
  expected_hash = compute_fnv1a64(packets, sizeof(packets));

  for (iteration = 0; iteration < WARMUP_BATCHES; iteration++) {
    memcpy(packets, source, sizeof(packets));
    prepare_cluster();
    if (decrypt_packets(keys, cluster) != BATCH_SIZE) {
      free_key_struct(keys);
      return 3;
    }
  }

  for (iteration = 0; iteration < MEASUREMENT_BATCHES; iteration++) {
    memcpy(packets, source, sizeof(packets));
    prepare_cluster();
#ifdef _WIN32
    QueryPerformanceCounter(&start);
#else
    clock_gettime(CLOCK_MONOTONIC, &start);
#endif
    if (decrypt_packets(keys, cluster) != BATCH_SIZE) {
      free_key_struct(keys);
      return 4;
    }
#ifdef _WIN32
    QueryPerformanceCounter(&end);
#else
    clock_gettime(CLOCK_MONOTONIC, &end);
#endif
    total_nanoseconds += elapsed_nanoseconds(&start, &end);
  }

  actual_hash = compute_fnv1a64(packets, sizeof(packets));
  if (actual_hash != expected_hash) {
    free_key_struct(keys);
    return 5;
  }

  {
    double packets_processed = (double)MEASUREMENT_BATCHES * BATCH_SIZE;
    double nanoseconds_per_packet = total_nanoseconds / packets_processed;
    double packets_per_second = 1000000000.0 / nanoseconds_per_packet;
    double megabits_per_second = (packets_per_second * PAYLOAD_SIZE * 8.0) / 1000000.0;
    printf("{\"format\":\"ffdecsa-compare-v1\",\"implementation\":\"ffdecsa-c\",\"runtime\":\"native-c\",\"architecture\":\"native\",\"parallelism\":%d,\"batch_packets\":%d,\"warmup_batches\":%d,\"measurement_batches\":%d,\"timed_scope\":\"decrypt_only\",\"copy_in_timed_scope\":false,\"payload_bytes_per_packet\":%d,\"packets_processed\":%.0f,\"elapsed_ns\":%llu,\"nanoseconds_per_packet\":%.3f,\"packets_per_second\":%.3f,\"megabits_per_second\":%.3f,\"managed_allocated_bytes\":0,\"output_fnv1a64\":\"%016llX\",\"verified\":true}\n",
        get_internal_parallelism(),
        BATCH_SIZE,
        WARMUP_BATCHES,
        MEASUREMENT_BATCHES,
        PAYLOAD_SIZE,
        packets_processed,
        (unsigned long long)total_nanoseconds,
        nanoseconds_per_packet,
        packets_per_second,
        megabits_per_second,
        (unsigned long long)actual_hash);
  }

  free_key_struct(keys);
  return 0;
}
