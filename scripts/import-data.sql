-- psql --single-transaction. Destination must be newly migrated and not receiving traffic.
\set ON_ERROR_STOP on
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM device.device_events) OR EXISTS (SELECT 1 FROM device.outbox_messages) THEN
        RAISE EXCEPTION 'Import rejected: destination contains events/outbox';
    END IF;
    IF EXISTS (SELECT 1 FROM device.devices WHERE device_id NOT IN (1001,1002)
        OR firmware_version <> 'sim-1.0.0' OR status <> 1 OR assigned_patient_id IS NOT NULL
        OR connectivity_status IS NOT NULL OR current_battery_level IS NOT NULL OR is_charging IS NOT NULL
        OR last_heartbeat_at IS NOT NULL OR monitoring_started_at IS NOT NULL OR last_connectivity_change_at IS NOT NULL) THEN
        RAISE EXCEPTION 'Import rejected: destination contains devices beyond untouched initial seeds';
    END IF;
END $$;
-- Only untouched migration demo seeds can reach this point; replaced atomically by the source inventory.
DELETE FROM device.devices WHERE device_id IN (1001,1002);
\copy device.devices (device_id, assigned_patient_id, firmware_version, status, connectivity_status, current_battery_level, is_charging, last_heartbeat_at, monitoring_started_at, last_connectivity_change_at) FROM 'devices.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy device.device_events (device_event_id, device_id, event_type, event_payload, is_resolved, created_at, resolved_at) FROM 'device_events.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy device.outbox_messages (outbox_message_id, type, payload, occurred_on, processed_on, error, retry_count) FROM 'outbox_messages.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM device.outbox_messages WHERE type NOT IN ('device-managment.low-battery-detected.v1', 'device-managment.low-battery-resolved.v1', 'device-managment.device-disconnected.v1', 'device-managment.device-reconnected.v1')) THEN
        RAISE EXCEPTION 'Import rejected: outbox includes messages from another process';
    END IF;
    IF EXISTS (SELECT 1 FROM device.device_events e LEFT JOIN device.devices d USING(device_id) WHERE d.device_id IS NULL) THEN
        RAISE EXCEPTION 'Import rejected: orphan device events';
    END IF;
END $$;
SELECT setval(pg_get_serial_sequence('device.device_events', 'device_event_id'), COALESCE(MAX(device_event_id),1), COUNT(*) > 0) FROM device.device_events;
SELECT setval(pg_get_serial_sequence('device.outbox_messages', 'outbox_message_id'), COALESCE(MAX(outbox_message_id),1), COUNT(*) > 0) FROM device.outbox_messages;
