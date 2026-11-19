using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    window_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    window_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exams", x => x.id);
                    table.CheckConstraint("ck_exams_duration_positive", "duration_minutes > 0");
                    table.CheckConstraint("ck_exams_retention_positive", "retention_days > 0");
                    table.CheckConstraint("ck_exams_window_order", "window_end_at > window_start_at");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    invite_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    invite_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invite_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_failed_login_count", "failed_login_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    correct_option_index = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.CheckConstraint("ck_questions_correct_option_in_range", "correct_option_index >= 0 AND correct_option_index < cardinality(options)");
                    table.CheckConstraint("ck_questions_min_two_options", "cardinality(options) >= 2");
                    table.ForeignKey(
                        name: "fk_questions_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    decay = table.Column<double>(type: "double precision", nullable: false),
                    threshold = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_settings", x => x.id);
                    table.CheckConstraint("ck_risk_settings_decay_non_negative", "decay >= 0");
                    table.CheckConstraint("ck_risk_settings_threshold_non_negative", "threshold >= 0");
                    table.CheckConstraint("ck_risk_settings_weight_non_negative", "weight >= 0");
                    table.ForeignKey(
                        name: "fk_risk_settings_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_exam_assignments_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    flags = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    initial_ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    risk_score = table.Column<double>(type: "double precision", nullable: false),
                    verification_attempts = table.Column<int>(type: "integer", nullable: false),
                    continue_justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_sessions", x => x.id);
                    table.CheckConstraint("ck_exam_sessions_risk_score_non_negative", "risk_score >= 0");
                    table.CheckConstraint("ck_exam_sessions_verification_attempts_non_negative", "verification_attempts >= 0");
                    table.ForeignKey(
                        name: "fk_exam_sessions_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_sessions_users_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_pairings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pairing_code_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    pairing_code_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    encrypted_session_key = table.Column<byte[]>(type: "bytea", nullable: true),
                    last_signal_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_pairings", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_pairings_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_option = table.Column<int>(type: "integer", nullable: true),
                    seq = table.Column<long>(type: "bigint", nullable: false),
                    client_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unmonitored = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answers", x => x.id);
                    table.CheckConstraint("ck_answers_selected_option_non_negative", "selected_option IS NULL OR selected_option >= 0");
                    table.CheckConstraint("ck_answers_seq_non_negative", "seq >= 0");
                    table.ForeignKey(
                        name: "fk_answers_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_answers_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    text_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                    table.CheckConstraint("ck_consent_records_session_matches_type", "(type = 'Session' AND exam_session_id IS NOT NULL) OR (type = 'Biometric' AND exam_session_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_consent_records_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consent_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_sec = table.Column<double>(type: "double precision", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    score_contribution = table.Column<double>(type: "double precision", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_false_alarm = table.Column<bool>(type: "boolean", nullable: false),
                    false_alarm_marked_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    false_alarm_marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.CheckConstraint("ck_events_confidence_range", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("ck_events_duration_non_negative", "duration_sec >= 0");
                    table.CheckConstraint("ck_events_false_alarm_marker", "is_false_alarm = false OR (false_alarm_marked_by_id IS NOT NULL AND false_alarm_marked_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_events_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_events_users_false_alarm_marked_by_id",
                        column: x => x.false_alarm_marked_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reviewer_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviewer_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviewer_decisions_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviewer_decisions_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    liveness_score = table.Column<double>(type: "double precision", nullable: false),
                    similarity = table.Column<double>(type: "double precision", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_verifications_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_pairing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nonce = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signature_valid = table.Column<bool>(type: "boolean", nullable: false),
                    indicators = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_signals", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_signals_agent_pairings_agent_pairing_id",
                        column: x => x.agent_pairing_id,
                        principalTable: "agent_pairings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reference_face_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encrypted_vector = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_version = table.Column<int>(type: "integer", nullable: false),
                    consent_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_face_templates", x => x.id);
                    table.CheckConstraint("ck_reference_face_templates_vector_size", "octet_length(encrypted_vector) BETWEEN 1 AND 8192");
                    table.ForeignKey(
                        name: "fk_reference_face_templates_consent_records_consent_record_id",
                        column: x => x.consent_record_id,
                        principalTable: "consent_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reference_face_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence_frames",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    key_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delete_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_frames", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_frames_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_pairings_exam_session_id",
                table: "agent_pairings",
                column: "exam_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_pairings_pairing_code_hash",
                table: "agent_pairings",
                column: "pairing_code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_signals_agent_pairing_id_nonce",
                table: "agent_signals",
                columns: new[] { "agent_pairing_id", "nonce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_answers_exam_session_id_question_id",
                table: "answers",
                columns: new[] { "exam_session_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answers_exam_session_id_seq",
                table: "answers",
                columns: new[] { "exam_session_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_answers_question_id",
                table: "answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target_type_target_id",
                table: "audit_logs",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_exam_session_id",
                table: "consent_records",
                column: "exam_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_user_id_type",
                table: "consent_records",
                columns: new[] { "user_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_events_exam_session_id_started_at",
                table: "events",
                columns: new[] { "exam_session_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_events_false_alarm_marked_by_id",
                table: "events",
                column: "false_alarm_marked_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_frames_delete_after",
                table: "evidence_frames",
                column: "delete_after");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_frames_event_id",
                table: "evidence_frames",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_frames_storage_key",
                table: "evidence_frames",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_assignments_exam_id_user_id",
                table: "exam_assignments",
                columns: new[] { "exam_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_assignments_user_id",
                table: "exam_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_candidate_id",
                table: "exam_sessions",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_exam_id_candidate_id",
                table: "exam_sessions",
                columns: new[] { "exam_id", "candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_state_ends_at",
                table: "exam_sessions",
                columns: new[] { "state", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_exam_id_order",
                table: "questions",
                columns: new[] { "exam_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_reference_face_templates_consent_record_id",
                table: "reference_face_templates",
                column: "consent_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_reference_face_templates_user_id",
                table: "reference_face_templates",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_decisions_exam_session_id",
                table: "reviewer_decisions",
                column: "exam_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_decisions_reviewer_id",
                table: "reviewer_decisions",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_settings_exam_id_event_type",
                table: "risk_settings",
                columns: new[] { "exam_id", "event_type" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_users_invite_token_hash",
                table: "users",
                column: "invite_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verifications_exam_session_id_created_at",
                table: "verifications",
                columns: new[] { "exam_session_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_signals");

            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "evidence_frames");

            migrationBuilder.DropTable(
                name: "exam_assignments");

            migrationBuilder.DropTable(
                name: "reference_face_templates");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reviewer_decisions");

            migrationBuilder.DropTable(
                name: "risk_settings");

            migrationBuilder.DropTable(
                name: "verifications");

            migrationBuilder.DropTable(
                name: "agent_pairings");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "exam_sessions");

            migrationBuilder.DropTable(
                name: "exams");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
