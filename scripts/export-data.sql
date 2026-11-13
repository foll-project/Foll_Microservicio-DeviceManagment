-- Execute from a new backup directory while device writers and link/unlink are stopped.
\set ON_ERROR_STOP on
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
\copy device.devices (device_id, assigned_patient_id, firmware_version, status, connectivity_status, current_battery_level, is_charging, last_heartbeat_at, monitoring_started_at, last_connectivity_change_at) TO 'devices.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy device.device_events (device_event_id, device_id, event_type, event_payload, is_resolved, created_at, resolved_at) TO 'device_events.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy (SELECT outbox_message_id, type, payload, occurred_on, processed_on, error, retry_count FROM device.outbox_messages WHERE type IN ('device-managment.low-battery-detected.v1', 'device-managment.low-battery-resolved.v1', 'device-managment.device-disconnected.v1', 'device-managment.device-reconnected.v1')) TO 'outbox_messages.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
COMMIT;
