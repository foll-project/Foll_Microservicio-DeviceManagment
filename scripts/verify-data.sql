SELECT count(*) AS devices, count(assigned_patient_id) AS linked_devices, min(device_id), max(device_id) FROM device.devices;
SELECT connectivity_status, count(*) FROM device.devices GROUP BY connectivity_status ORDER BY connectivity_status;
SELECT event_type, is_resolved, count(*) FROM device.device_events GROUP BY event_type, is_resolved ORDER BY event_type, is_resolved;
SELECT type, count(*) AS total, count(*) FILTER (WHERE processed_on IS NULL) AS pending FROM device.outbox_messages
WHERE type IN ('device-managment.low-battery-detected.v1', 'device-managment.low-battery-resolved.v1', 'device-managment.device-disconnected.v1', 'device-managment.device-reconnected.v1') GROUP BY type ORDER BY type;
SELECT assigned_patient_id, count(*) FROM device.devices WHERE assigned_patient_id IS NOT NULL GROUP BY assigned_patient_id HAVING count(*) > 1;
SELECT count(*) AS orphan_events FROM device.device_events e LEFT JOIN device.devices d USING(device_id) WHERE d.device_id IS NULL;
SELECT min(last_heartbeat_at), max(last_heartbeat_at) FROM device.devices;
